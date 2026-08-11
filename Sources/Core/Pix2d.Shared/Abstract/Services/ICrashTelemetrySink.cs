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

    /// <summary>
    /// Mirrors "what the user was doing" into the sink's <em>ambient</em> scope, so a crash the app
    /// never sees still carries it. <see cref="CaptureFatal"/> / <see cref="CaptureNonFatal"/> attach
    /// context at capture time, which only works for crashes that reach a managed handler — a native
    /// signal (e.g. SIGSEGV inside libSkiaSharp on the render path) bypasses them entirely and is
    /// captured by the platform's own crash handler, which can only read the ambient scope. Without
    /// this, those events arrive with no Pix2d context at all and are untriageable.
    /// <para>
    /// Called on every executed command, so implementations must be cheap and never throw. Only the
    /// two low-cardinality values are mirrored (not the whole op-log): on Android each write is synced
    /// across the JNI boundary to the native layer, and the full session tail is recovered from the
    /// on-disk session crumb instead.
    /// </para>
    /// </summary>
    void UpdateLiveContext(string? lastCommandName, string? appContext)
    {
        // Default no-op: a sink without an ambient scope (or a head that doesn't ship telemetry)
        // loses nothing — the capture-time context in CaptureFatal/CaptureNonFatal is unaffected.
    }

    /// <summary>
    /// Reports a crash that killed a <b>previous</b> process and was reconstructed from the OS exit
    /// record on the next launch (native crash / ANR). This needs its own path — neither of the two
    /// above fits:
    /// <list type="bullet">
    /// <item><see cref="CaptureFatal"/> would mark the <em>current</em>, perfectly healthy session as
    /// crashed (auto session tracking keys off the event), corrupting crash-free-session rates.</item>
    /// <item><see cref="CaptureNonFatal"/> tags the event as handled, so a real crash would be filed
    /// as an ordinary error.</item>
    /// <item>Both take an <see cref="Exception"/>, and there is none — synthesizing one gets it
    /// stamped with the stack of the reporting call site, which would group every recovered crash on
    /// every device into a single issue named after this method.</item>
    /// </list>
    /// Implementations must therefore group on <paramref name="fingerprint"/> explicitly and set the
    /// release from <see cref="CrashReportSummary.AppVersion"/> (the version that crashed, which is
    /// not necessarily the one running now).
    /// </summary>
    void CaptureRecovered(CrashReportSummary summary, string fingerprint)
    {
        // Default no-op: heads without telemetry keep the local report and the recovery banner.
    }
}
