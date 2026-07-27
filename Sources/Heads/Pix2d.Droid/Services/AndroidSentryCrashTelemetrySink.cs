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
