#nullable enable
using Pix2d.Primitives.Crash;

namespace Pix2d.Abstract.Services;

public interface ICrashReportService
{
    bool HasPendingCrashReport { get; }
    CrashReportSummary? PendingCrashReport { get; }

    /// <summary>
    /// Whether the previous run ended through a deliberate, graceful shutdown (the one-shot
    /// <see cref="MarkCleanExit"/> marker was present at launch). False on a crash / OS kill, and on
    /// the very first launch. Captured once during startup detection so callers see a stable verdict
    /// even after the underlying marker is consumed. Used to decide whether to surface the
    /// crash-recovery banner after autosave restores a session.
    /// </summary>
    bool PreviousShutdownWasClean { get; }

    TelemetryConsent TelemetryConsent { get; }
    void SetTelemetryConsent(TelemetryConsent consent);

    /// <summary>
    /// Raised whenever <see cref="SetTelemetryConsent"/> changes the stored consent. The bootstrapper
    /// subscribes so it can bring up analytics / crash telemetry the moment the user allows it at
    /// runtime (e.g. from the first-launch consent dialog), without waiting for the next launch.
    /// </summary>
    event Action<TelemetryConsent>? TelemetryConsentChanged;

    void MarkLaunchStarted();
    void MarkLaunchCompleted();

    /// <summary>
    /// Records that the app is shutting down deliberately at the user's request (e.g. the Android
    /// double-back exit, which self-kills the process). Without this marker the OS-reported
    /// termination is indistinguishable from a signal-based native crash and the next launch shows a
    /// phantom crash report. Must be called just before the process is terminated.
    /// </summary>
    void MarkCleanExit();

    /// <summary>Loads the most recently saved crash report, if any.</summary>
    CrashReportSummary? LoadLatestReport();

    /// <summary>Captures an unhandled/critical exception and writes a normalized envelope.</summary>
    CrashReportSummary CaptureFatal(Exception exception, string source);

    /// <summary>
    /// Reports a handled, recoverable exception (e.g. a command that threw) to remote telemetry with
    /// its source attached. Consent-gated and rate-limited per exception signature; writes no crash
    /// envelope and shows no crash UI. Callers remain responsible for local logging.
    /// </summary>
    void CaptureHandled(Exception exception, string source);

    /// <summary>Clears the "pending" flag so the auto dialog won't show on next launch (the report file remains).</summary>
    void DismissPending();

    /// <summary>Track the last command/operation name for crash context.</summary>
    void RecordLastCommand(string commandName);

    /// <summary>Path to the latest report file as plain text, if available.</summary>
    string? GetLatestReportFilePath();
}
