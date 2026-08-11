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

        // Order matters: read the previous session's crumb *before* this session starts writing its
        // own, because detection below is what consumes it. Starting our own crumb immediately after
        // keeps the invariant "the crumb on disk always belongs to the session that wrote it last",
        // which is what makes attribution on the next launch sound.
        _previousCrumb = TryReadSessionCrumb();
        BeginSessionCrumb();

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

    // ISettingsService.Get resolves the key by reflection on every call, and consent is now read on
    // hot paths (every executed command, via RecordLastCommand). It only ever changes through
    // SetTelemetryConsent, so cache it after the first read and refresh it there.
    private TelemetryConsent? _consentCache;

    public TelemetryConsent TelemetryConsent
    {
        get
        {
            if (_consentCache is { } cached)
                return cached;

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

            var resolved = raw is (int)TelemetryConsent.Allowed or (int)TelemetryConsent.Denied
                ? (TelemetryConsent)raw
                : TelemetryConsent.Unset;
            _consentCache = resolved;
            return resolved;
        }
    }

    public void SetTelemetryConsent(TelemetryConsent consent)
    {
        // Update the cache before persisting: the in-memory switch must take effect even if the
        // settings write below fails (the existing contract — see the catch).
        _consentCache = consent;

        // Withdrawing consent must *drop* the queued recovered crash, not merely skip it. Otherwise
        // a later Denied → Allowed toggle would resurrect and upload a report the user had already
        // declined to send.
        if (consent != TelemetryConsent.Allowed)
            ClearPendingTelemetryForward();

        try
        {
            _settingsService.Set(nameof(AppSettings.TelemetryConsent), (int)consent);
        }
        catch (Exception ex)
        {
            // Persisting the choice must never throw at the caller: this runs from the consent dialog's
            // button handler, and an escaping exception used to abort it before it closed the dialog,
            // leaving the user with two dead buttons. Failing to store the value only means consent stays
            // Unset and is asked again next launch, while the in-memory switch below still takes effect.
            Logger.LogException(ex);
        }

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
        // Called from the Android lifecycle (OnPause) as well as the startup pipeline, so this is
        // also the app's "about to be backgrounded" signal — the moment the process is most likely
        // to be killed without warning. Persist the crumb synchronously here even though the
        // settings write below is skipped on repeat calls.
        WriteSessionCrumb();

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

    public void RecordLastCommand(string commandName)
    {
        _lastCommandName = commandName;
        MirrorLiveContextToTelemetry(commandName);
        ScheduleSessionCrumbWrite();
    }

    /// <summary>
    /// Pushes the last command + app-state snapshot into the telemetry sink's ambient scope. This is
    /// what makes a *native* crash triageable: a SIGSEGV never reaches CaptureFatal, so the only
    /// context such an event can carry is whatever was already sitting in the sink's scope when the
    /// process died. Consent-gated and fully guarded — a telemetry failure must never affect command
    /// execution.
    /// </summary>
    private void MirrorLiveContextToTelemetry(string commandName)
    {
        try
        {
            if (TelemetryConsent != TelemetryConsent.Allowed)
                return;

            if (ResolveTelemetrySink() is not { IsInitialized: true } sink)
                return;

            sink.UpdateLiveContext(commandName, SafeAppContext());
        }
        catch
        {
        }
    }

    // The sink is a singleton that is always registered but initialized late (after consent), so the
    // instance is cached once while IsInitialized is re-checked per call.
    private ICrashTelemetrySink? _telemetrySink;
    private bool _telemetrySinkResolved;

    private ICrashTelemetrySink? ResolveTelemetrySink()
    {
        if (_telemetrySinkResolved)
            return _telemetrySink;

        _telemetrySinkResolved = true;
        try
        {
            _telemetrySink = _serviceProvider.GetService(typeof(ICrashTelemetrySink)) as ICrashTelemetrySink;
        }
        catch
        {
            _telemetrySink = null;
        }

        return _telemetrySink;
    }

    // One event per (exception type + source) per window keeps a repeatedly-failing action (button
    // mashing, a throwing render loop) from flooding telemetry while still recording the first hit;
    // Sentry-side occurrence counts come from separate signatures/sessions, not from this app spamming.
    private static readonly TimeSpan HandledDedupeWindow = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, DateTime> _handledSignatures = new();

    public void CaptureHandled(Exception exception, string source)
    {
        try
        {
            if (TelemetryConsent != TelemetryConsent.Allowed)
                return;

            if (ResolveTelemetrySink() is not { IsInitialized: true } sink)
                return;

            // Unwrap aggregates and stamp a capture-site stack on frame-less exceptions before both the
            // signature and the report are derived from it — otherwise a stackless error is untriageable.
            exception = PrepareForCapture(exception);

            var signature = $"{exception.GetType().FullName}|{source}";
            var now = DateTime.UtcNow;
            lock (_handledSignatures)
            {
                if (_handledSignatures.TryGetValue(signature, out var last) && now - last < HandledDedupeWindow)
                    return;

                // The signature space is tiny in practice (distinct failing call sites); reset rather
                // than grow unbounded if something pathological generates unique sources.
                if (_handledSignatures.Count > 128)
                    _handledSignatures.Clear();
                _handledSignatures[signature] = now;
            }

            // Build the same rich summary as the fatal path so a handled error carries the text stack
            // (with capture-site fallback), exception chain, session op-log tail and app-state snapshot —
            // without persisting an envelope or surfacing crash UI. This is what turns a frame-less
            // handled error (e.g. an AOT-stripped NRE) into something triageable.
            var summary = BuildSummary(exception, source, isImplicit: false);
            sink.CaptureNonFatal(summary, exception);
        }
        catch
        {
            // Telemetry must never throw back into the caller's error handling.
        }
    }

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
        // Unwrap AggregateException wrappers (the TaskScheduler.UnobservedTaskException path hands us
        // one whose own frames are empty) and stamp a capture-site stack when the exception carries
        // none, so the local envelope and the forwarded Sentry event both point at real code.
        exception = PrepareForCapture(exception);

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

            if (ResolveTelemetrySink() is not { IsInitialized: true } sink)
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
                // A SIGKILL is the OEM low-memory killer evicting a backgrounded app, which Android
                // also reports as Signaled. It still surfaces locally (the user did lose work), but
                // forwarding it would flood telemetry with phantom "native crashes" that no code
                // change can fix.
                PromoteImplicit(BuildImplicitSummary(exit, ReadAndConsumeFatalLog()),
                    forwardToTelemetry: !exit.IsLowMemoryKill);
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
                    PromoteImplicit(BuildImplicitSummary(null, fatalLog), forwardToTelemetry: true);
                    return;
                }

                ClearLaunchInProgress();
                return;
            }

            // 4) Nothing flagged, but a stray Fatal.log from a pre-bootstrap crash may still exist.
            var orphanFatal = ReadAndConsumeFatalLog();
            if (!string.IsNullOrWhiteSpace(orphanFatal))
                PromoteImplicit(BuildImplicitSummary(null, orphanFatal), forwardToTelemetry: true);
        }
        catch
        {
            HasPendingCrashReport = false;
            PendingCrashReport = null;
        }
    }

    private void PromoteImplicit(CrashReportSummary summary, bool forwardToTelemetry)
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

            // Queue it for telemetry by *filename*, persisted. The sink is not up yet at this point
            // (this runs from the constructor), and the OS exit record that produced this summary has
            // already been consumed — so an in-memory hand-off would lose the crash for good if
            // consent only arrives on a later launch.
            if (forwardToTelemetry)
                _settingsService.Set(nameof(AppSettings.PendingTelemetryForwardId), fileName);

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
            StackTrace = BuildStackText(exception),
            ExceptionChain = BuildExceptionChain(exception),
            SessionOperationLog = SafeSessionLog(),
            LogTail = ReadLogTail(),
            StartupDocument = SafeStartupDocument(),
            LastCommandName = _lastCommandName,
            AppContext = SafeAppContext(),
        };
        return summary;
    }

    /// <summary>
    /// Normalizes an exception before it is summarized and forwarded to telemetry so the remote event
    /// carries a locatable managed stack. Two gaps otherwise produce frame-less Sentry events that are
    /// impossible to pin to code (exactly the "errors without a stack trace" seen in the wild):
    /// <list type="number">
    /// <item>An <see cref="AggregateException"/> wrapper — e.g. from
    /// <c>TaskScheduler.UnobservedTaskException</c> — whose own frames are empty while the real culprit
    /// sits in <c>InnerException</c>. We flatten a single-inner aggregate to that culprit so its type,
    /// message and stack become what gets reported.</item>
    /// <item>An exception that was constructed/surfaced but never actually thrown, so
    /// <see cref="Exception.StackTrace"/> is <c>null</c> (Sentry extracts frames from the exception
    /// object, so it shows only the type + message). We re-throw it once here to stamp the current
    /// capture-site call stack — the handler/command chain that led into the report.</item>
    /// </list>
    /// Guarded so an exception that already carries frames is returned untouched (never restamped).
    /// </summary>
    private static Exception PrepareForCapture(Exception exception)
    {
        var ex = exception;

        if (ex is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();
            if (flattened.InnerExceptions.Count == 1)
                ex = flattened.InnerExceptions[0];
        }

        if (string.IsNullOrEmpty(ex.StackTrace))
        {
            try
            {
                throw ex;
            }
            catch (Exception stamped)
            {
                return stamped;
            }
        }

        return ex;
    }

    /// <summary>
    /// Returns the exception's own captured stack, or — when it is empty (frame-less: common on
    /// trimmed/AOT Android builds, or thrown deep in framework/native code with no managed frames) —
    /// a marker plus the current <see cref="Environment.StackTrace"/> at the reporting call site. That
    /// fallback at least records which handler/command surfaced the error and the managed call chain
    /// leading into the capture, so an otherwise-untriageable event points somewhere.
    /// </summary>
    private static string BuildStackText(Exception exception)
    {
        var stack = exception.StackTrace;
        if (!string.IsNullOrWhiteSpace(stack))
            return stack;

        try
        {
            return "(exception carried no stack — capture-site trace below)\n" + Environment.StackTrace;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Compact, always-present snapshot of app state at capture time. Best-effort and fully guarded —
    /// crash handlers run on arbitrary threads and reading observable state may race, so any failure
    /// degrades to a partial/empty string rather than throwing back into the crash path.
    /// </summary>
    private string SafeAppContext()
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("plat=").Append(SafePlatform());

            try { sb.Append(" tool=").Append(_appState.ToolsState.CurrentToolKey ?? "-"); } catch { }

            try
            {
                sb.Append(" tabs=").Append(_appState.LoadedProjects.Count)
                    .Append('@').Append(_appState.ActiveProjectIndex);
            }
            catch { }

            try
            {
                var p = _appState.CurrentProject;
                if (p != null)
                {
                    sb.Append(" ctx=").Append(p.CurrentContextType)
                        .Append(" new=").Append(p.IsNewProject)
                        .Append(" sel=").Append(p.HasSelection);

                    var edited = p.CurrentEditedNode;
                    if (edited != null)
                        sb.Append(" canvas=").Append((int)edited.Size.Width)
                            .Append('x').Append((int)edited.Size.Height);
                }
            }
            catch { }

            try
            {
                sb.Append(" frame=").Append(_appState.SpriteEditorState.CurrentFrameIndex)
                    .Append('/').Append(_appState.SpriteEditorState.FramesCount);
            }
            catch { }

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private CrashReportSummary BuildImplicitSummary(ProcessExitDetails? exit, string? fatalLog)
    {
        var hasExitCrash = exit is { LikelyCrash: true };
        var hasFatalLog = !string.IsNullOrWhiteSpace(fatalLog);

        string source;
        string exceptionType;
        string message;
        string? fingerprint = null;
        var stack = new StringBuilder();

        if (hasExitCrash)
        {
            source = $"ProcessExit:{exit!.Reason}";

            // Derive a real signature from the tombstone instead of reporting the OS's generic
            // sentence. Without this every native crash — a Skia fault, a mono abort, an ANR —
            // arrives under one identical title and collapses into a single, unactionable group.
            var signature = NativeCrashSignature.Derive(exit);
            exceptionType = signature.SignalName;
            message = signature.Title;
            fingerprint = signature.Fingerprint;

            if (!string.IsNullOrWhiteSpace(signature.FaultCode) || !string.IsNullOrWhiteSpace(signature.AbortMessage))
            {
                // Kept as detail, deliberately out of the signature: si_code varies between runs of
                // the same bug (SEGV_MAPERR vs SEGV_ACCERR depending on the stale pointer's value).
                stack.AppendLine("=== Fault detail ===");
                if (!string.IsNullOrWhiteSpace(signature.FaultCode))
                    stack.AppendLine($"code: {signature.FaultCode}");
                if (!string.IsNullOrWhiteSpace(signature.AbortMessage))
                    stack.AppendLine($"abort message: {signature.AbortMessage}");
                if (!string.IsNullOrWhiteSpace(exit.Description))
                    stack.AppendLine($"os description: {exit.Description}");
                stack.AppendLine();
            }

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

            // Key on the first line of the log (the exception type + message) rather than on a fixed
            // sentence. These crashes were never forwarded at crash time — the telemetry sink did not
            // exist yet — so this is their only route out, and a constant title would pile every
            // unrelated startup failure into one group.
            var firstLine = FirstMeaningfulLine(fatalLog!);
            message = firstLine is { Length: > 0 }
                ? $"Crash before services were ready: {TelemetryMessageNormalizer.Normalize(firstLine)}"
                : "A fatal error was captured before the crash services were ready.";
            fingerprint = $"prebootstrap|{TelemetryMessageNormalizer.Normalize(firstLine)}";
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

        // Everything below describes the session that DIED, so it must come from the persisted crumb,
        // not from this freshly-started process. Reading the live values here is what made recovered
        // crash reports arrive with an empty op-log, no last command, and — worst of all — the
        // version currently installed rather than the version that actually crashed.
        var crumb = ResolveCrumbFor(exit);

        return new CrashReportSummary
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow,
            AppVersion = crumb?.AppVersion is { Length: > 0 } crashedVersion ? crashedVersion : SafeAppVersion(),
            Platform = crumb?.Platform is { Length: > 0 } crashedPlatform ? crashedPlatform : SafePlatform(),
            Source = source,
            IsImplicit = true,
            ExceptionType = exceptionType,
            Message = message,
            TelemetryFingerprint = fingerprint,
            StackTrace = stack.ToString(),
            ExceptionChain = exit?.Description ?? string.Empty,
            SessionOperationLog = crumb?.OpLogTail ?? string.Empty,
            // Not ReadLogTail(): by now the recovering process has already written its own startup
            // lines into the same file, so the tail is a mix of two sessions.
            LogTail = string.Empty,
            StartupDocument = SafeStartupDocument(),
            LastCommandName = crumb?.LastCommandName,
            AppContext = crumb?.AppContext,
            ContextAgeMs = ComputeContextAge(crumb, exit),
        };
    }

    /// <summary>
    /// Milliseconds between the crumb's last refresh and the reported death — i.e. how much of the
    /// crash the recovered context actually describes.
    /// </summary>
    private static long? ComputeContextAge(SessionCrumb? crumb, ProcessExitDetails? exit)
    {
        if (crumb == null || exit == null || exit.TimestampMs <= 0 || crumb.UpdatedUtcMs <= 0)
            return null;
        return exit.TimestampMs - crumb.UpdatedUtcMs;
    }

    #region Session crumb — the previous session speaking for itself

    private const string CrumbFileName = "session_crumb.json";
    private const int CrumbOpLogItems = 60;

    /// <summary>
    /// Debounce interval. Long enough that a burst of commands costs one write, short enough that the
    /// command immediately before a native crash almost always made it to disk — which is the entire
    /// point, since that command is the payload.
    /// </summary>
    private static readonly TimeSpan CrumbWriteDebounce = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Beyond this, context is treated as unavailable rather than reported as if it described the
    /// crash: an app that sat idle for a day before being killed has a crumb whose op-log has nothing
    /// to do with the death, and a misleading context is worse than none.
    /// </summary>
    private static readonly TimeSpan CrumbMaxUsefulAge = TimeSpan.FromHours(24);

    private readonly SessionCrumb? _previousCrumb;
    private string _sessionId = string.Empty;
    private long _sessionStartedUtcMs;
    private bool _crumbEnabled;
    private Timer? _crumbTimer;
    private int _crumbWriteScheduled;

    /// <summary>
    /// Starts this session's crumb. Gated on the platform actually being able to report a previous
    /// process death: on desktop nothing ever reads the crumb back (there is no
    /// <see cref="IProcessExitInfoProvider"/>), and a single shared file would anyway be overwritten
    /// by whichever of several concurrently-running desktop instances wrote last, making attribution
    /// meaningless. The first write happens right away so that "the crumb on disk belongs to the last
    /// session that ran" holds even for a crash seconds after launch.
    /// </summary>
    private void BeginSessionCrumb()
    {
        try
        {
            _crumbEnabled = _platformStuffService is IProcessExitInfoProvider;
            if (!_crumbEnabled)
                return;

            _sessionId = Guid.NewGuid().ToString("N");
            _sessionStartedUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _crumbTimer = new Timer(_ => OnCrumbTimerTick(), null, Timeout.Infinite, Timeout.Infinite);

            ScheduleSessionCrumbWrite();
        }
        catch
        {
            _crumbEnabled = false;
        }
    }

    /// <summary>
    /// Marks the crumb dirty and arms a single delayed write. Deliberately does <em>not</em> restart
    /// the timer on each call: continuous activity would then keep pushing the write into the future
    /// and nothing would ever reach disk.
    /// </summary>
    private void ScheduleSessionCrumbWrite()
    {
        if (!_crumbEnabled) return;

        try
        {
            if (Interlocked.CompareExchange(ref _crumbWriteScheduled, 1, 0) != 0)
                return;

            _crumbTimer?.Change(CrumbWriteDebounce, Timeout.InfiniteTimeSpan);
        }
        catch
        {
        }
    }

    private void OnCrumbTimerTick()
    {
        Interlocked.Exchange(ref _crumbWriteScheduled, 0);
        WriteSessionCrumb();
    }

    /// <summary>
    /// Rewrites the crumb atomically: temp file then rename, the same idiom the settings and autosave
    /// stores use. Without it a SIGKILL mid-write leaves torn JSON on disk at precisely the launch
    /// that needs to read it.
    /// </summary>
    private void WriteSessionCrumb()
    {
        if (!_crumbEnabled) return;

        try
        {
            var crumb = new SessionCrumb
            {
                SessionId = _sessionId,
                StartedUtcMs = _sessionStartedUtcMs,
                UpdatedUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                AppVersion = SafeAppVersion(),
                Platform = SafePlatform(),
                LastCommandName = _lastCommandName,
                OpLogTail = SafeSessionLogTail(),
                AppContext = SafeAppContext(),
            };

            var folder = GetCrashFolder();
            EnsureFolder(folder);

            var path = Path.Combine(folder, CrumbFileName);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(crumb, _jsonOptions));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Best-effort: losing a crumb costs context on a future crash, nothing more.
        }
    }

    private SessionCrumb? TryReadSessionCrumb()
    {
        try
        {
            var path = Path.Combine(GetCrashFolder(), CrumbFileName);
            if (!File.Exists(path))
                return null;

            return JsonSerializer.Deserialize<SessionCrumb>(File.ReadAllText(path), _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the previous session's crumb only when it can plausibly describe the given death.
    /// Rejects a crumb refreshed <em>after</em> the reported exit (that is this session's own crumb,
    /// not the dead one's) and a crumb far older than the exit (stale idle context).
    /// </summary>
    private SessionCrumb? ResolveCrumbFor(ProcessExitDetails? exit)
    {
        var crumb = _previousCrumb;
        if (crumb == null)
            return null;

        // Same session id ⇒ we are looking at our own crumb; it describes nothing about a past death.
        if (!string.IsNullOrEmpty(_sessionId) && crumb.SessionId == _sessionId)
            return null;

        if (exit is { TimestampMs: > 0 } && crumb.UpdatedUtcMs > 0)
        {
            var age = exit.TimestampMs - crumb.UpdatedUtcMs;
            if (age < 0 || age > CrumbMaxUsefulAge.TotalMilliseconds)
                return null;
        }

        return crumb;
    }

    private static string SafeSessionLogTail()
    {
        try { return SessionLogger.GetSessionOperationLogTail(CrumbOpLogItems); }
        catch { return string.Empty; }
    }

    private static string? FirstMeaningfulLine(string text)
    {
        try
        {
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                // Skip the report header the plain-text fallback writes before the exception itself.
                if (trimmed.Length == 0 || trimmed.StartsWith("===", StringComparison.Ordinal))
                    continue;
                return trimmed;
            }
        }
        catch
        {
        }

        return null;
    }

    #endregion

    #region Deferred forwarding of recovered crashes

    private bool _pendingForwardFlushed;

    public void FlushPendingTelemetry()
    {
        try
        {
            // Guard against the double-send the bootstrapper's wiring invites: consent that is already
            // Allowed at launch flushes once from InitOptionalTelemetry, and re-confirming Allowed in
            // Settings raises TelemetryConsentChanged (which fires unconditionally, without change
            // detection) and would flush the same crash again.
            if (_pendingForwardFlushed)
                return;

            if (TelemetryConsent != TelemetryConsent.Allowed)
                return;

            var fileName = _settingsService.Get<string>(nameof(AppSettings.PendingTelemetryForwardId));
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            if (ResolveTelemetrySink() is not { IsInitialized: true } sink)
                return;

            var path = Path.Combine(GetCrashFolder(), fileName);
            if (!File.Exists(path))
            {
                ClearPendingTelemetryForward();
                return;
            }

            var summary = JsonSerializer.Deserialize<CrashReportSummary>(File.ReadAllText(path), _jsonOptions);
            if (summary == null)
            {
                ClearPendingTelemetryForward();
                return;
            }

            _pendingForwardFlushed = true;

            // Clear before sending: the Sentry SDK persists the envelope to its offline cache, so an
            // enqueue is as good as delivered, whereas clearing afterwards risks re-sending forever if
            // the send path throws.
            ClearPendingTelemetryForward();

            // Off the calling thread. The consent path runs synchronously inside the dialog's button
            // handler, and the send path can block (the fatal path ends with a 2s SentrySdk.Flush) —
            // doing this inline would freeze the UI on the consent dialog.
            // Never drop a report for want of a fingerprint: an envelope written by an older build
            // has none, and coarse grouping by source still beats silence.
            var fingerprint = string.IsNullOrEmpty(summary.TelemetryFingerprint)
                ? $"recovered|{summary.Source}"
                : summary.TelemetryFingerprint;

            Task.Run(() =>
            {
                try
                {
                    sink.CaptureRecovered(summary, fingerprint);
                }
                catch
                {
                }
            });
        }
        catch
        {
            // Telemetry must never break startup or the consent dialog.
        }
    }

    private void ClearPendingTelemetryForward()
    {
        TrySet<string?>(nameof(AppSettings.PendingTelemetryForwardId), null);
    }

    #endregion

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

            // TargetSite (the throwing method) and Source (the throwing assembly) are captured on the
            // exception object at throw time, independently of the StackTrace string — so on a
            // frame-less event they are often the only pointer at *where* the error came from. Guarded:
            // resolving TargetSite can itself throw on trimmed/AOT builds.
            try
            {
                var site = current.TargetSite;
                if (site != null)
                    sb.AppendLine($"      at {site.DeclaringType?.FullName ?? "?"}.{site.Name}");
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(current.Source))
                sb.AppendLine($"      source: {current.Source}");

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
