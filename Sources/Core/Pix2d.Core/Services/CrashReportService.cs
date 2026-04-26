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
    private bool _alreadyCapturedFatal;
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

    public CrashTelemetryConsent TelemetryConsent
    {
        get
        {
            var raw = _settingsService.Get<int>(nameof(AppSettings.CrashTelemetryConsent));
            return raw is (int)CrashTelemetryConsent.Allowed or (int)CrashTelemetryConsent.Denied
                ? (CrashTelemetryConsent)raw
                : CrashTelemetryConsent.Unset;
        }
    }

    public void SetTelemetryConsent(CrashTelemetryConsent consent)
    {
        _settingsService.Set(nameof(AppSettings.CrashTelemetryConsent), (int)consent);
    }

    public void MarkLaunchStarted()
    {
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
        try
        {
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
            var id = _settingsService.Get<string>(nameof(AppSettings.LastCrashReportId));
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var path = Path.Combine(GetCrashFolder(), id);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CrashReportSummary>(json, _jsonOptions);
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
            var id = _settingsService.Get<string>(nameof(AppSettings.LastCrashReportId));
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var path = Path.Combine(GetCrashFolder(), id);
            return File.Exists(path) ? path : null;
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
                if (_alreadyCapturedFatal) return summary;
                _alreadyCapturedFatal = true;

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
            if (TelemetryConsent != CrashTelemetryConsent.Allowed)
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
            var crashedSilently = _settingsService.Get<bool>(nameof(AppSettings.LaunchInProgress));

            if (hasReport)
            {
                PendingCrashReport = LoadLatestReport();
                HasPendingCrashReport = PendingCrashReport != null;
            }
            else if (crashedSilently)
            {
                // No envelope on disk but the previous launch never reached "completed".
                PendingCrashReport = BuildImplicitSummary();
                HasPendingCrashReport = true;

                try
                {
                    var folder = GetCrashFolder();
                    EnsureFolder(folder);
                    var fileName = $"{PendingCrashReport.Timestamp:yyyyMMdd_HHmmss}_silent.json";
                    var fullPath = Path.Combine(folder, fileName);
                    File.WriteAllText(fullPath, JsonSerializer.Serialize(PendingCrashReport, _jsonOptions));
                    File.WriteAllText(Path.ChangeExtension(fullPath, ".txt"), PendingCrashReport.FormatForDisplay());
                    _settingsService.Set(nameof(AppSettings.LastCrashReportId), fileName);
                    _settingsService.Set(nameof(AppSettings.HasPendingCrashReport), true);
                }
                catch
                {
                }
            }
        }
        catch
        {
            HasPendingCrashReport = false;
            PendingCrashReport = null;
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

    private CrashReportSummary BuildImplicitSummary()
    {
        return new CrashReportSummary
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow,
            AppVersion = SafeAppVersion(),
            Platform = SafePlatform(),
            Source = "PreviousLaunchInterrupted",
            IsImplicit = true,
            ExceptionType = "UnknownInterruption",
            Message = "Previous launch did not finish cleanly.",
            StackTrace = string.Empty,
            ExceptionChain = string.Empty,
            SessionOperationLog = string.Empty,
            LogTail = ReadLogTail(),
            StartupDocument = SafeStartupDocument(),
            LastCommandName = null,
        };
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
