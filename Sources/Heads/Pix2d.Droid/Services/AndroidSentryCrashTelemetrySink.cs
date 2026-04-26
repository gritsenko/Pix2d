#nullable enable
using System;
using Pix2d.Abstract.Services;
using Pix2d.Primitives.Crash;

namespace Pix2d.Droid.Services;

/// <summary>
/// Opt-in critical-crash sink for Android.
///
/// V1 plumbing: this class wires user consent, deduplication, and a one-place fatal filter.
/// To enable real Sentry delivery later:
/// 1. Add &lt;PackageReference Include="Sentry" Version="..." /&gt; to Pix2d.Droid.csproj.
/// 2. Inside <see cref="Initialize"/>, call <c>SentrySdk.Init(o =&gt; { o.Dsn = ...; o.AutoSessionTracking = false; o.IsGlobalModeEnabled = true; })</c>
///    using a DSN sourced from build configuration (NEVER hardcoded).
/// 3. Inside <see cref="CaptureFatal"/>, call <c>SentrySdk.CaptureException(exception)</c> with
///    tags <c>app_version</c>, <c>platform</c>, <c>crash_report_id</c> set from the summary.
/// 4. Drop performance/profiling/session features (off by default in this code path).
///
/// Until Sentry is added the sink stays initialised but no-op, so consent, plumbing, and the local
/// crash report flow are still validated end-to-end on Android.
/// </summary>
public sealed class AndroidSentryCrashTelemetrySink : ICrashTelemetrySink
{
    private bool _initialized;

    public bool IsInitialized => _initialized;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            Android.Util.Log.Info("Pix2d.Crash", "Crash telemetry sink initialized (no-op until Sentry SDK is wired).");
        }
        catch
        {
        }
    }

    public void Shutdown()
    {
        _initialized = false;
    }

    public void CaptureFatal(CrashReportSummary summary, Exception exception)
    {
        if (!_initialized) return;
        try
        {
            // Filter point: only fatal/critical events arrive here. Non-fatal Logger.LogException
            // calls bypass this sink entirely.
            Android.Util.Log.Error("Pix2d.Crash",
                $"FATAL {summary.ExceptionType}: {summary.Message} (id={summary.Id})");
        }
        catch
        {
        }
    }
}
