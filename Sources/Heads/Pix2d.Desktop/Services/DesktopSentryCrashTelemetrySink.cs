#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Pix2d.Abstract.Services;
using Pix2d.Primitives.Crash;
using Sentry;

namespace Pix2d.Desktop.Services;

/// <summary>
/// Opt-in critical-crash sink for the desktop heads (Windows / Linux / macOS, incl. the MS Store
/// WAP bundle). Mirrors <c>AndroidSentryCrashTelemetrySink</c>: the DSN is injected at build time via
/// the <c>SentryDsn</c> MSBuild property (set from the <c>SENTRY_DSN</c> env var / CI secret) and
/// embedded as an <see cref="AssemblyMetadataAttribute"/> on the head assembly. When no DSN is
/// provided the sink stays initialised but no-op, so the local crash report flow keeps working in
/// dev builds. Only fatal/critical crashes are forwarded, and only after the user has explicitly
/// allowed anonymous crash reporting (gated in <see cref="ICrashReportService"/>).
/// </summary>
public sealed class DesktopSentryCrashTelemetrySink : ICrashTelemetrySink
{
    private bool _initialized;
    private bool _sentryActive;

    public bool IsInitialized => _initialized;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dsn = ReadDsnFromAssemblyMetadata();
            if (string.IsNullOrWhiteSpace(dsn))
            {
                Logger.Log("Sentry DSN not configured; telemetry sink will run no-op.");
                return;
            }

            SentrySdk.Init(o =>
            {
                o.Dsn = dsn;
                // Auto session tracking: SDK starts a session on Init() and ends it on shutdown,
                // sending session envelopes with duration / error count / exit status. The Sentry SDK
                // registers its own ProcessExit hook to flush and end the session on a normal exit;
                // Shutdown() (→ SentrySdk.Close()) also ends it. Without this no sessions reach Sentry.
                o.AutoSessionTracking = true;
                o.IsGlobalModeEnabled = true;
                // Offline cache: if the Sentry host is unreachable at crash time, the envelope is
                // persisted locally and re-sent on the next launch instead of being lost.
                o.CacheDirectoryPath = GetCacheDirectory();
            });
            _sentryActive = true;
            Logger.Log("Sentry crash telemetry sink initialized.");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public void Shutdown()
    {
        try
        {
            if (_sentryActive)
                SentrySdk.Close();
        }
        catch
        {
        }
        _sentryActive = false;
        _initialized = false;
    }

    public void CaptureFatal(CrashReportSummary summary, Exception exception)
    {
        if (!_initialized) return;
        try
        {
            Logger.Log($"FATAL {summary.ExceptionType}: {summary.Message} (id={summary.Id})");

            if (!_sentryActive) return;

            SentrySdk.CaptureException(exception, scope =>
            {
                scope.Level = SentryLevel.Fatal;
                scope.SetTag("app_version", summary.AppVersion);
                scope.SetTag("platform", string.IsNullOrEmpty(summary.Platform) ? "desktop" : summary.Platform);
                scope.SetTag("crash_report_id", summary.Id);
                scope.SetTag("crash_source", summary.Source);
                if (summary.IsImplicit)
                    scope.SetTag("crash_implicit", "true");
                if (!string.IsNullOrEmpty(summary.LastCommandName))
                    scope.SetTag("last_command", summary.LastCommandName);
                // Raw managed context survives even when SDK frame extraction yields nothing
                // (trimmed builds): the text stack, the full inner-exception chain, and the last
                // user operations leading up to the crash.
                if (!string.IsNullOrEmpty(summary.StackTrace))
                    scope.SetExtra("stack_trace_text", Tail(summary.StackTrace, 8 * 1024));
                if (!string.IsNullOrEmpty(summary.ExceptionChain))
                    scope.SetExtra("exception_chain", Tail(summary.ExceptionChain, 4 * 1024));
                if (!string.IsNullOrEmpty(summary.SessionOperationLog))
                    scope.SetExtra("session_op_log_tail", Tail(summary.SessionOperationLog, 4 * 1024));
                if (!string.IsNullOrEmpty(summary.AppContext))
                    scope.SetExtra("app_context", summary.AppContext);
            });
            SentrySdk.Flush(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
    }

    public void CaptureNonFatal(CrashReportSummary summary, Exception exception)
    {
        if (!_initialized || !_sentryActive) return;
        try
        {
            SentrySdk.CaptureException(exception, scope =>
            {
                scope.Level = SentryLevel.Error;
                scope.SetTag("handled", "true");
                scope.SetTag("error_source", summary.Source);
                scope.SetTag("app_version", summary.AppVersion);
                scope.SetTag("platform", string.IsNullOrEmpty(summary.Platform) ? "desktop" : summary.Platform);
                if (!string.IsNullOrEmpty(summary.LastCommandName))
                    scope.SetTag("last_command", summary.LastCommandName);
                // Same rich context as CaptureFatal — critical when the exception is frame-less and the
                // SDK extracts no stack: the text stack (with capture-site fallback), inner-exception
                // chain, recent operations and the app-state snapshot are the only way to locate it.
                if (!string.IsNullOrEmpty(summary.StackTrace))
                    scope.SetExtra("stack_trace_text", Tail(summary.StackTrace, 8 * 1024));
                if (!string.IsNullOrEmpty(summary.ExceptionChain))
                    scope.SetExtra("exception_chain", Tail(summary.ExceptionChain, 4 * 1024));
                if (!string.IsNullOrEmpty(summary.SessionOperationLog))
                    scope.SetExtra("session_op_log_tail", Tail(summary.SessionOperationLog, 4 * 1024));
                if (!string.IsNullOrEmpty(summary.AppContext))
                    scope.SetExtra("app_context", summary.AppContext);
            });
        }
        catch
        {
        }
    }

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text.Substring(text.Length - maxChars);

    private static string? ReadDsnFromAssemblyMetadata()
    {
        return typeof(DesktopSentryCrashTelemetrySink).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "SentryDsn", StringComparison.Ordinal))?.Value;
    }

    // Same LocalApplicationData\Pix2d root the rest of the app uses (see PlatformStuffService).
    private static string GetCacheDirectory() =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pix2d", "SentryCache");
}
