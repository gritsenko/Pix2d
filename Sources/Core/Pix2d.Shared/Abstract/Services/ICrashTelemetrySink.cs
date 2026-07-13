#nullable enable
using Pix2d.Primitives.Crash;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Optional sink that forwards fatal/critical crash envelopes to a remote service (e.g. Sentry).
/// Heads register this only when telemetry is appropriate for the platform; the crash service
/// gates calls on user consent so non-fatal logs are never forwarded.
/// </summary>
public interface ICrashTelemetrySink
{
    bool IsInitialized { get; }
    void Initialize();
    void Shutdown();
    void CaptureFatal(CrashReportSummary summary, Exception exception);

    /// <summary>
    /// Forwards a handled (non-fatal) exception with its origin, e.g. a failing command. Unlike
    /// <see cref="CaptureFatal"/> this writes no local crash envelope and never surfaces crash UI —
    /// it exists purely so recoverable errors are still visible remotely with their context attached.
    /// Takes the same <see cref="CrashReportSummary"/> as <see cref="CaptureFatal"/> so a handled error
    /// carries the same rich context (text stack incl. capture-site fallback, exception chain, session
    /// op-log tail, app-state snapshot) — essential when the exception itself is frame-less.
    /// </summary>
    void CaptureNonFatal(CrashReportSummary summary, Exception exception);
}
