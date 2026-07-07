#nullable enable
using Pix2d.Primitives.Crash;

namespace Pix2d.Abstract.Services;

public interface ICrashReportService
{
    bool HasPendingCrashReport { get; }
    CrashReportSummary? PendingCrashReport { get; }

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

    /// <summary>Clears the "pending" flag so the auto dialog won't show on next launch (the report file remains).</summary>
    void DismissPending();

    /// <summary>Track the last command/operation name for crash context.</summary>
    void RecordLastCommand(string commandName);

    /// <summary>Path to the latest report file as plain text, if available.</summary>
    string? GetLatestReportFilePath();
}
