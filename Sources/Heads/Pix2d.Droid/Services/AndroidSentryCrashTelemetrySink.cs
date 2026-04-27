#nullable enable
using Pix2d.Abstract.Services;
using Pix2d.Primitives.Crash;
using Sentry;
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
                o.AutoSessionTracking = false;
                o.IsGlobalModeEnabled = true;
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
                scope.SetTag("app_version", summary.AppVersion);
                scope.SetTag("platform", string.IsNullOrEmpty(summary.Platform) ? "android" : summary.Platform);
                scope.SetTag("crash_report_id", summary.Id);
                scope.SetTag("crash_source", summary.Source);
                if (summary.IsImplicit)
                    scope.SetTag("crash_implicit", "true");
                if (!string.IsNullOrEmpty(summary.LastCommandName))
                    scope.SetTag("last_command", summary.LastCommandName);
            });
            SentrySdk.Flush(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
    }

    private static string? ReadDsnFromAssemblyMetadata()
    {
        return typeof(AndroidSentryCrashTelemetrySink).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "SentryDsn", StringComparison.Ordinal))?.Value;
    }
}
