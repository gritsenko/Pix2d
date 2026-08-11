#nullable enable
using Pix2d.Abstract.Services;
using Pix2d.Primitives.Crash;
using Sentry;
using Sentry.Protocol;
using System;
using System.Linq;
using System.Reflection;

namespace Pix2d.Droid.Services;

/// <summary>
/// Opt-in critical-crash sink for Android. The DSN is injected at build time via the
/// <c>SentryDsn</c> MSBuild property (set from the <c>SENTRY_DSN</c> env var / GitHub secret) and
/// embedded as an <see cref="AssemblyMetadataAttribute"/>. When no DSN is provided the sink stays
/// initialised but no-op, so the local crash report flow keeps working in dev builds.
/// </summary>
public sealed class AndroidSentryCrashTelemetrySink : ICrashTelemetrySink
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
                Android.Util.Log.Info("Pix2d.Crash", "Sentry DSN not configured; telemetry sink will run no-op.");
                return;
            }

            SentrySdk.Init(o =>
            {
                o.Dsn = dsn;
                // Auto session tracking: SDK starts a session on Init() and, with global mode on,
                // ends it on Close() (called from Shutdown()) — so Sentry receives session envelopes
                // with duration / error count / exit status. Without this no sessions reach the server.
                o.AutoSessionTracking = true;
                o.IsGlobalModeEnabled = true;
                // Sync the managed scope down to the embedded Android/NDK SDK. Without this the
                // native crash handler cannot see anything set from C#: the SDK docs are explicit
                // that "EnableScopeSync must be set true for the scope to be synced". This is what
                // makes UpdateLiveContext() worth anything for a SIGSEGV in native code (e.g. the
                // Skia render path), which never reaches a managed handler and therefore carries
                // only whatever the native layer already had in its own scope.
                o.EnableScopeSync = true;
                // Explicit rather than relying on defaults — these two are the entire reason a
                // native crash / ANR is reported at all, and a silent upstream default flip would
                // switch the app back to reporting nothing without any visible change here.
                o.Native.EnableNdk = true;
                o.Native.AnrEnabled = true;
                // Offline cache: if the Sentry host is unreachable at crash time, the envelope is
                // persisted locally and re-sent on the next launch instead of being lost.
                var cacheDir = Android.App.Application.Context.CacheDir?.AbsolutePath;
                if (!string.IsNullOrEmpty(cacheDir))
                    o.CacheDirectoryPath = System.IO.Path.Combine(cacheDir, "sentry");
                o.SetBeforeSend(NormalizeMessagesForGrouping);
            });
            _sentryActive = true;
            Android.Util.Log.Info("Pix2d.Crash", "Sentry crash telemetry sink initialized.");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("Pix2d.Crash", $"Failed to initialize Sentry: {ex}");
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
            Android.Util.Log.Error("Pix2d.Crash",
                $"FATAL {summary.ExceptionType}: {summary.Message} (id={summary.Id})");

            if (!_sentryActive) return;

            SentrySdk.CaptureException(exception, scope =>
            {
                scope.Level = SentryLevel.Fatal;
                scope.SetTag("app_version", summary.AppVersion);
                scope.SetTag("platform", string.IsNullOrEmpty(summary.Platform) ? "android" : summary.Platform);
                scope.SetTag("crash_report_id", summary.Id);
                scope.SetTag("crash_source", summary.Source);
                if (summary.IsImplicit)
                    scope.SetTag("crash_implicit", "true");
                if (!string.IsNullOrEmpty(summary.LastCommandName))
                    scope.SetTag("last_command", summary.LastCommandName);
                // Raw managed context survives even when SDK frame extraction yields nothing
                // (trimmed/AOT builds): the text stack, the full inner-exception chain, and the last
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
                scope.SetTag("platform", string.IsNullOrEmpty(summary.Platform) ? "android" : summary.Platform);
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

    /// <summary>
    /// Sends a crash that killed a previous process, reconstructed on this launch from the OS exit
    /// record. Built as a bare <see cref="SentryEvent"/> rather than through
    /// <c>CaptureException</c> on purpose:
    /// <list type="bullet">
    /// <item>there is no exception to capture, and synthesizing one would get it stamped with the
    /// stack of this method, grouping every recovered crash everywhere into one issue;</item>
    /// <item><see cref="Fingerprint"/> is set explicitly, so grouping comes from the tombstone
    /// signature instead of Sentry's stack/message heuristics, which have nothing to work with;</item>
    /// <item><see cref="SentryEvent.Release"/> is set from the summary, so the crash is attributed to
    /// the version that <em>crashed</em> and not to whatever is installed now.</item>
    /// </list>
    /// The event is not marked as an unhandled crash mechanism, which keeps the current (healthy)
    /// session out of the crash-free-sessions calculation.
    /// </summary>
    public void CaptureRecovered(CrashReportSummary summary, string fingerprint)
    {
        if (!_initialized) return;
        try
        {
            Android.Util.Log.Error("Pix2d.Crash",
                $"RECOVERED {summary.ExceptionType}: {summary.Message} (id={summary.Id})");

            if (!_sentryActive) return;

            var evt = new SentryEvent
            {
                Level = SentryLevel.Fatal,
                Message = summary.Message,
                Fingerprint = [fingerprint],
            };

            if (!string.IsNullOrEmpty(summary.AppVersion))
                evt.Release = summary.AppVersion;

            evt.SetTag("crash_recovered", "true");
            evt.SetTag("crash_source", summary.Source);
            evt.SetTag("signal", summary.ExceptionType);
            evt.SetTag("platform_reported", string.IsNullOrEmpty(summary.Platform) ? "android" : summary.Platform);
            evt.SetTag("crash_report_id", summary.Id);
            if (!string.IsNullOrEmpty(summary.LastCommandName))
                evt.SetTag("last_command", summary.LastCommandName);

            // The SDK cannot backdate an event, so the death time travels as data. Without it the
            // event looks like it happened at the next launch, which can be days later.
            evt.SetExtra("crash_detected_utc", summary.Timestamp.ToString("O"));
            if (summary.ContextAgeMs is { } age)
                evt.SetExtra("context_age_ms", age);

            // The tombstone itself: unsymbolicated frames are still the only description of the fault.
            if (!string.IsNullOrEmpty(summary.StackTrace))
                evt.SetExtra("exit_trace", Tail(summary.StackTrace, 16 * 1024));
            if (!string.IsNullOrEmpty(summary.SessionOperationLog))
                evt.SetExtra("session_op_log_tail", Tail(summary.SessionOperationLog, 4 * 1024));
            if (!string.IsNullOrEmpty(summary.AppContext))
                evt.SetExtra("app_context", summary.AppContext);
            if (!string.IsNullOrEmpty(summary.ExceptionChain))
                evt.SetExtra("os_description", Tail(summary.ExceptionChain, 2 * 1024));

            SentrySdk.CaptureEvent(evt);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Keeps the ambient (global) scope current so a crash captured outside the managed handlers —
    /// an NDK signal or an ANR — still says what the user was doing. Paired with
    /// <c>EnableScopeSync</c> in <see cref="Initialize"/>, which is what pushes these values across
    /// to the native SDK; without that flag they would only decorate managed events.
    /// </summary>
    public void UpdateLiveContext(string? lastCommandName, string? appContext)
    {
        if (!_sentryActive) return;
        try
        {
            SentrySdk.ConfigureScope(scope =>
            {
                if (!string.IsNullOrEmpty(lastCommandName))
                    scope.SetTag("last_command", lastCommandName);
                // Extra, not a tag: app_context is high-cardinality (canvas size, frame index, tab
                // count) and would blow up Sentry's tag index while adding nothing searchable.
                if (!string.IsNullOrEmpty(appContext))
                    scope.SetExtra("app_context", appContext);
            });

            // A trail of commands, not just the last one: for a native crash the preceding few
            // actions are usually what identifies the path into the faulting code.
            if (!string.IsNullOrEmpty(lastCommandName))
                SentrySdk.AddBreadcrumb(lastCommandName, category: "command");
        }
        catch
        {
            // Live context is best-effort decoration; it must never disturb command execution.
        }
    }

    /// <summary>
    /// Rewrites the outgoing event's exception/message text through
    /// <see cref="TelemetryMessageNormalizer"/> so one *kind* of failure maps to one signature instead
    /// of one per byte count / path / GUID (aggregation keys on the message — see the normalizer's
    /// docs). Mirrors <c>DesktopSentryCrashTelemetrySink</c>. The raw text is kept in
    /// <c>original_message</c>, and <c>exception_chain</c>/<c>stack_trace_text</c> are left untouched,
    /// so the concrete values are still one click away when triaging.
    /// </summary>
    private static SentryEvent NormalizeMessagesForGrouping(SentryEvent e)
    {
        try
        {
            var original = e.SentryExceptions?.FirstOrDefault()?.Value
                           ?? e.Message?.Formatted ?? e.Message?.Message;
            var changed = false;

            foreach (var sentryException in e.SentryExceptions ?? Enumerable.Empty<SentryException>())
            {
                if (string.IsNullOrEmpty(sentryException.Value)) continue;
                var normalized = TelemetryMessageNormalizer.Normalize(sentryException.Value);
                if (normalized == sentryException.Value) continue;
                sentryException.Value = normalized;
                changed = true;
            }

            var messageText = e.Message?.Formatted ?? e.Message?.Message;
            if (!string.IsNullOrEmpty(messageText))
            {
                var normalized = TelemetryMessageNormalizer.Normalize(messageText);
                if (normalized != messageText)
                {
                    e.Message = normalized;
                    changed = true;
                }
            }

            if (changed && !string.IsNullOrEmpty(original))
                e.SetExtra("original_message", original);
        }
        catch
        {
            // Never let signature shaping drop an event.
        }
        return e;
    }

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text.Substring(text.Length - maxChars);

    private static string? ReadDsnFromAssemblyMetadata()
    {
        return typeof(AndroidSentryCrashTelemetrySink).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "SentryDsn", StringComparison.Ordinal))?.Value;
    }
}
