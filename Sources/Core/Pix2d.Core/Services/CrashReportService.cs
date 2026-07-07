#nullable enable
using System.Text;
using System.Text.Json;
using Pix2d.Abstract.Services;
using Pix2d.Common;
using Pix2d.Primitives.Crash;
using Pix2d.State;

namespace Pix2d.Services;

public class CrashReportService : ICrashReportService
{
    private const string CrashFolderName = "CrashReports";
    private const int MaxStoredReports = 5;
    private const int LogTailBytes = 32 * 1024;

    private readonly IPlatformStuffService _platformStuffService;
    private readonly ISettingsService _settingsService;
    private readonly AppState _appState;
    private readonly IServiceProvider _serviceProvider;

    private readonly object _lock = new();
    private DateTime _lastFatalCaptureUtc = DateTime.MinValue;
    private bool _launchCompletedThisRun;

    // A single crash routinely reaches more than one global handler within a few milliseconds
    // (e.g. on Android AndroidEnvironment.UnhandledExceptionRaiser + AppDomain.UnhandledException),
    // and we don't want two reports for it. A genuinely new crash — or a repeated debug-panel
    // simulation while the app stays alive — happens seconds apart and is still captured.
    private static readonly TimeSpan FatalDedupeWindow = TimeSpan.FromSeconds(2);
    private string? _lastCommandName;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = false,
    };

    public CrashReportService(
        IPlatformStuffService platformStuffService,
        ISettingsService settingsService,
        AppState appState,
        IServiceProvider serviceProvider)
    {
        _platformStuffService = platformStuffService;
        _settingsService = settingsService;
        _appState = appState;
        _serviceProvider = serviceProvider;

        DetectPendingFromPreviousLaunch();
    }

    public bool HasPendingCrashReport { get; private set; }
    public CrashReportSummary? PendingCrashReport { get; private set; }

    // Assume clean until detection proves otherwise, so a failure in DetectPendingFromPreviousLaunch
    // never manufactures a spurious "recovered after a crash" banner.
    public bool PreviousShutdownWasClean { get; private set; } = true;

    public event Action<TelemetryConsent>? TelemetryConsentChanged;

    // Pre-3.9 stored consent under a crash-only key; read it once and fold it into the unified key so
    // a user who already answered the old crash dialog isn't prompted again.
    private const string LegacyConsentKey = "CrashTelemetryConsent";

    public TelemetryConsent TelemetryConsent
    {
        get
        {
            var raw = _settingsService.Get<int>(nameof(AppSettings.TelemetryConsent));
            if (raw == 0)
            {
                var legacy = _settingsService.Get<int>(LegacyConsentKey);
                if (legacy is (int)TelemetryConsent.Allowed or (int)TelemetryConsent.Denied)
                {
                    raw = legacy;
                    TrySet(nameof(AppSettings.TelemetryConsent), raw);
                }
            }

            return raw is (int)TelemetryConsent.Allowed or (int)TelemetryConsent.Denied
                ? (TelemetryConsent)raw
                : TelemetryConsent.Unset;
        }
    }

    public void SetTelemetryConsent(TelemetryConsent consent)
    {
        _settingsService.Set(nameof(AppSettings.TelemetryConsent), (int)consent);
        try
        {
            TelemetryConsentChanged?.Invoke(consent);
        }
        catch
        {
            // A consent listener must never break the setter (analytics/telemetry init is best-effort).
        }
    }

    public void MarkLaunchStarted()
    {
        _launchCompletedThisRun = false;
        try
        {
            _settingsService.Set(nameof(AppSettings.LaunchInProgress), true);
        }
        catch
        {
            // logging itself shouldn't crash startup
        }
    }

    public void MarkLaunchCompleted()
    {
        // Idempotent within a run: this is called both from the startup pipeline and from the
        // Android lifecycle (OnPause) as a safety net, and we don't want a settings write each time.
        if (_launchCompletedThisRun)
            return;
        _launchCompletedThisRun = true;
        try
        {
            _settingsService.Set(nameof(AppSettings.LaunchInProgress), false);
        }
        catch
        {
        }
    }

    public void MarkCleanExit()
    {
        // Persisted synchronously (SettingsService.Set writes through to disk) so the marker is
        // guaranteed on disk before the caller terminates the process. Best-effort: a deliberate
        // exit must never be blocked by a settings write failure.
        try
        {
            _settingsService.Set(nameof(AppSettings.CleanExitRequested), true);
            _settingsService.Set(nameof(AppSettings.LaunchInProgress), false);
        }
        catch
        {
        }
    }

    public void RecordLastCommand(string commandName) => _lastCommandName = commandName;

    public CrashReportSummary? LoadLatestReport()
    {
        try
        {
            foreach (var path in EnumerateReportCandidates())
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var summary = JsonSerializer.Deserialize<CrashReportSummary>(json, _jsonOptions);
                    if (summary == null)
                        continue;

                    TrySyncLatestReportId(path);
                    return summary;
                }
                catch
                {
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public string? GetLatestReportFilePath()
    {
        try
        {
            var path = EnumerateReportCandidates().FirstOrDefault();
            if (path == null)
                return null;

            TrySyncLatestReportId(path);
            return path;
        }
        catch
        {
            return null;
        }
    }

    public CrashReportSummary CaptureFatal(Exception exception, string source)
    {
        var summary = BuildSummary(exception, source, isImplicit: false);

        // Best-effort persist; never throw from the crash path.
        try
        {
            lock (_lock)
            {
                if (summary.Timestamp - _lastFatalCaptureUtc < FatalDedupeWindow)
                {
                    PendingCrashReport ??= summary;
                    HasPendingCrashReport = PendingCrashReport != null;
                    return summary;
                }

                _lastFatalCaptureUtc = summary.Timestamp;
                PendingCrashReport = summary;
                HasPendingCrashReport = true;

                var folder = GetCrashFolder();
                EnsureFolder(folder);

                var fileName = $"{summary.Timestamp:yyyyMMdd_HHmmss}_{ShortHash(summary.Id)}.json";
                var fullPath = Path.Combine(folder, fileName);

                File.WriteAllText(fullPath, JsonSerializer.Serialize(summary, _jsonOptions));
                File.WriteAllText(Path.ChangeExtension(fullPath, ".txt"), summary.FormatForDisplay());

                _settingsService.Set(nameof(AppSettings.LastCrashReportId), fileName);
                _settingsService.Set(nameof(AppSettings.HasPendingCrashReport), true);
                _settingsService.Set(nameof(AppSettings.LaunchInProgress), false);

                TrimOldReports(folder);
            }
        }
        catch
        {
            // Last-resort fallback to plain text — guarantees something is on disk.
            TryWritePlainTextFallback(summary, exception);
        }

        TryForwardToTelemetry(summary, exception);

        return summary;
    }

    private void TryForwardToTelemetry(CrashReportSummary summary, Exception exception)
    {
        try
        {
            if (TelemetryConsent != TelemetryConsent.Allowed)
                return;

            var sink = _serviceProvider.GetService(typeof(ICrashTelemetrySink)) as ICrashTelemetrySink;
            if (sink == null || !sink.IsInitialized)
                return;

            sink.CaptureFatal(summary, exception);
        }
        catch
        {
            // Telemetry must never throw out of the crash path.
        }
    }

    public void DismissPending()
    {
        HasPendingCrashReport = false;
        PendingCrashReport = null;
        try
        {
            _settingsService.Set(nameof(AppSettings.HasPendingCrashReport), false);
        }
        catch
        {
        }
    }

    private void DetectPendingFromPreviousLaunch()
    {
        try
        {
            var hasReport = _settingsService.Get<bool>(nameof(AppSettings.HasPendingCrashReport));
            var launchInProgress = _settingsService.Get<bool>(nameof(AppSettings.LaunchInProgress));

            // The previous run ended through a deliberate, user-initiated shutdown (e.g. the Android
            // double-back exit self-kills the process, which the OS reports as SIGNALED/EXIT_SELF).
            // Consume the one-shot marker immediately so it can never suppress a genuine crash on a
            // later launch.
            var cleanExit = _settingsService.Get<bool>(nameof(AppSettings.CleanExitRequested));
            PreviousShutdownWasClean = cleanExit;
            if (cleanExit)
                TrySet(nameof(AppSettings.CleanExitRequested), false);

            // Ask the OS why the previous process died (Android API 30+). This is the only way to
            // observe native crashes / ANRs / OS kills that bypass the managed exception handlers.
            var exit = TryGetLastProcessExit();
            var lastHandled = _settingsService.Get<long>(nameof(AppSettings.LastHandledProcessExitTimestamp));
            var exitIsNew = exit != null && exit.TimestampMs > lastHandled;
            if (exitIsNew)
                TrySet(nameof(AppSettings.LastHandledProcessExitTimestamp), exit!.TimestampMs);

            // 1) A full envelope was written by CaptureFatal on the previous run — richest report, wins.
            //    A genuine fatal sets this flag, so it still surfaces even after a later clean exit.
            if (hasReport)
            {
                PendingCrashReport = LoadLatestReport();
                HasPendingCrashReport = PendingCrashReport != null;
                return;
            }

            // The shutdown was deliberate: the OS-reported termination is expected, so don't
            // manufacture a report from it or from the interrupted-launch heuristic.
            if (cleanExit)
            {
                ClearLaunchInProgress();
                return;
            }

            // 2) The OS attributes the previous exit to a crash. Covers native crashes and managed
            //    crashes that slipped past the handlers — both during launch and mid-session.
            if (exitIsNew && exit!.LikelyCrash)
            {
                PromoteImplicit(BuildImplicitSummary(exit, ReadAndConsumeFatalLog()));
                return;
            }

            // 3) The previous launch started but never reported "completed".
            if (launchInProgress)
            {
                // The OS says it was a non-crash exit (user closed it, background low-memory kill,
                // etc.). Don't manufacture a phantom crash report.
                if (exitIsNew && !exit!.LikelyCrash)
                {
                    ClearLaunchInProgress();
                    return;
                }

                // No OS verdict (desktop has no IProcessExitInfoProvider; Android < API 30). A stuck
                // "launch in progress" flag on its own is too weak a signal to surface a crash dialog
                // from — on desktop it's routinely left set when a debug session is stopped or the
                // process is killed, which produced the phantom "empty" crash report on the next
                // launch. Only promote when we actually recovered a pre-bootstrap Fatal.log; otherwise
                // just clear the flag (still consuming any Fatal.log so it can't linger).
                var fatalLog = ReadAndConsumeFatalLog();
                if (!string.IsNullOrWhiteSpace(fatalLog))
                {
                    PromoteImplicit(BuildImplicitSummary(null, fatalLog));
                    return;
                }

                ClearLaunchInProgress();
                return;
            }

            // 4) Nothing flagged, but a stray Fatal.log from a pre-bootstrap crash may still exist.
            var orphanFatal = ReadAndConsumeFatalLog();
            if (!string.IsNullOrWhiteSpace(orphanFatal))
                PromoteImplicit(BuildImplicitSummary(null, orphanFatal));
        }
        catch
        {
            HasPendingCrashReport = false;
            PendingCrashReport = null;
        }
    }

    private void PromoteImplicit(CrashReportSummary summary)
    {
        PendingCrashReport = summary;
        HasPendingCrashReport = true;

        try
        {
            var folder = GetCrashFolder();
            EnsureFolder(folder);
            var fileName = $"{summary.Timestamp:yyyyMMdd_HHmmss}_silent.json";
            var fullPath = Path.Combine(folder, fileName);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(summary, _jsonOptions));
            File.WriteAllText(Path.ChangeExtension(fullPath, ".txt"), summary.FormatForDisplay());
            _settingsService.Set(nameof(AppSettings.LastCrashReportId), fileName);
            _settingsService.Set(nameof(AppSettings.HasPendingCrashReport), true);
            _settingsService.Set(nameof(AppSettings.LaunchInProgress), false);
            TrimOldReports(folder);
        }
        catch
        {
        }
    }

    private void ClearLaunchInProgress()
    {
        TrySet(nameof(AppSettings.LaunchInProgress), false);
    }

    private void TrySet<T>(string key, T value)
    {
        try { _settingsService.Set(key, value); }
        catch { }
    }

    private ProcessExitDetails? TryGetLastProcessExit()
    {
        try
        {
            return (_platformStuffService as IProcessExitInfoProvider)?.GetLastProcessExitDetails();
        }
        catch
        {
            return null;
        }
    }

    private CrashReportSummary BuildSummary(Exception exception, string source, bool isImplicit)
    {
        var summary = new CrashReportSummary
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow,
            AppVersion = SafeAppVersion(),
            Platform = SafePlatform(),
            Source = source,
            IsImplicit = isImplicit,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message ?? string.Empty,
            StackTrace = exception.StackTrace ?? string.Empty,
            ExceptionChain = BuildExceptionChain(exception),
            SessionOperationLog = SafeSessionLog(),
            LogTail = ReadLogTail(),
            StartupDocument = SafeStartupDocument(),
            LastCommandName = _lastCommandName,
        };
        return summary;
    }

    private CrashReportSummary BuildImplicitSummary(ProcessExitDetails? exit, string? fatalLog)
    {
        var hasExitCrash = exit is { LikelyCrash: true };
        var hasFatalLog = !string.IsNullOrWhiteSpace(fatalLog);

        string source;
        string exceptionType;
        string message;
        var stack = new StringBuilder();

        if (hasExitCrash)
        {
            source = $"ProcessExit:{exit!.Reason}";
            exceptionType = exit.Reason;
            message = string.IsNullOrWhiteSpace(exit.Description)
                ? "The previous process terminated abnormally (reported by the OS)."
                : exit.Description;

            if (!string.IsNullOrWhiteSpace(exit.TraceText))
            {
                stack.AppendLine("=== OS exit trace (native / ANR) ===");
                stack.AppendLine(exit.TraceText);
            }
        }
        else if (hasFatalLog)
        {
            source = "PreBootstrapFatalLog";
            exceptionType = "CapturedBeforeServicesReady";
            message = "A fatal error was captured before the crash services were ready.";
        }
        else
        {
            source = "PreviousLaunchInterrupted";
            exceptionType = "UnknownInterruption";
            message = "Previous launch did not finish cleanly.";
        }

        if (hasFatalLog)
        {
            if (stack.Length > 0)
                stack.AppendLine();
            stack.AppendLine("=== Fatal.log (pre-bootstrap handler) ===");
            stack.AppendLine(fatalLog);
        }

        return new CrashReportSummary
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow,
            AppVersion = SafeAppVersion(),
            Platform = SafePlatform(),
            Source = source,
            IsImplicit = true,
            ExceptionType = exceptionType,
            Message = message,
            StackTrace = stack.ToString(),
            ExceptionChain = exit?.Description ?? string.Empty,
            SessionOperationLog = SafeSessionLog(),
            LogTail = ReadLogTail(),
            StartupDocument = SafeStartupDocument(),
            LastCommandName = _lastCommandName,
        };
    }

    private const int FatalLogMaxChars = 16 * 1024;

    /// <summary>
    /// Reads (and then deletes) any plain-text Fatal.log left by the early/pre-bootstrap exception
    /// handlers. These are written when a crash happens before <see cref="ICrashReportService"/> is
    /// available, so they're the only record of those crashes — fold them into the report once.
    /// </summary>
    private static string? ReadAndConsumeFatalLog()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Fatal.log"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pix2dLogs", "Fatal.log"),
            };

            string? newest = null;
            var newestTime = DateTime.MinValue;

            foreach (var path in candidates)
            {
                try
                {
                    if (!File.Exists(path))
                        continue;

                    var time = File.GetLastWriteTimeUtc(path);
                    var text = File.ReadAllText(path);
                    if (text.Length > FatalLogMaxChars)
                        text = text.Substring(text.Length - FatalLogMaxChars);

                    if (time >= newestTime)
                    {
                        newestTime = time;
                        newest = text;
                    }

                    File.Delete(path); // one-shot: consume so it isn't re-attached on every launch
                }
                catch
                {
                }
            }

            return newest;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildExceptionChain(Exception ex)
    {
        var sb = new StringBuilder();
        var depth = 0;
        var current = ex;
        while (current != null && depth < 8)
        {
            sb.AppendLine($"[{depth}] {current.GetType().FullName}: {current.Message}");
            current = current.InnerException;
            depth++;
        }
        return sb.ToString();
    }

    private string SafeAppVersion()
    {
        try { return _platformStuffService.GetAppVersion(); }
        catch { return "unknown"; }
    }

    private string SafePlatform()
    {
        try { return _platformStuffService.CurrentPlatform.ToString(); }
        catch { return "unknown"; }
    }

    private string? SafeStartupDocument()
    {
        try { return _appState.CurrentProject?.File?.Path; }
        catch { return null; }
    }

    private static string SafeSessionLog()
    {
        try { return SessionLogger.GetSessionOperationLogText(); }
        catch { return string.Empty; }
    }

    private static string ReadLogTail()
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(folder, "Pix2dLogs", "pix2d_log.txt");
            if (!File.Exists(path))
                return string.Empty;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var len = fs.Length;
            var startOffset = Math.Max(0, len - LogTailBytes);
            fs.Seek(startOffset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private IEnumerable<string> EnumerateReportCandidates()
    {
        var folder = GetCrashFolder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var id = _settingsService.Get<string>(nameof(AppSettings.LastCrashReportId));
        if (!string.IsNullOrWhiteSpace(id))
        {
            var preferredPath = Path.Combine(folder, id);
            if (File.Exists(preferredPath) && seen.Add(preferredPath))
                yield return preferredPath;
        }

        if (!Directory.Exists(folder))
            yield break;

        foreach (var file in new DirectoryInfo(folder)
                     .GetFiles("*.json")
                     .OrderByDescending(file => file.LastWriteTimeUtc))
        {
            if (seen.Add(file.FullName))
                yield return file.FullName;
        }
    }

    private void TrySyncLatestReportId(string fullPath)
    {
        try
        {
            var fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var current = _settingsService.Get<string>(nameof(AppSettings.LastCrashReportId));
            if (string.Equals(current, fileName, StringComparison.OrdinalIgnoreCase))
                return;

            _settingsService.Set(nameof(AppSettings.LastCrashReportId), fileName);
        }
        catch
        {
        }
    }

    private string GetCrashFolder()
    {
        var root = _platformStuffService.GetAppFolderPath();
        return Path.Combine(root, CrashFolderName);
    }

    private static void EnsureFolder(string folder)
    {
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
    }

    private static string ShortHash(string id) => id.Length >= 8 ? id.Substring(0, 8) : id;

    private static void TrimOldReports(string folder)
    {
        try
        {
            var files = new DirectoryInfo(folder)
                .GetFiles("*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(MaxStoredReports)
                .ToList();

            foreach (var f in files)
            {
                try
                {
                    f.Delete();
                    var txt = Path.ChangeExtension(f.FullName, ".txt");
                    if (File.Exists(txt)) File.Delete(txt);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private void TryWritePlainTextFallback(CrashReportSummary summary, Exception exception)
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(folder, "Pix2dLogs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Fatal.log");
            File.WriteAllText(path, summary.FormatForDisplay());
        }
        catch
        {
            try
            {
                var personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                File.WriteAllText(Path.Combine(personal, "Fatal.log"), exception.ToString());
            }
            catch
            {
            }
        }
    }
}
