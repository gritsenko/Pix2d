#nullable enable
using Pix2d.Primitives.Crash;

namespace Pix2d.Abstract.Services;

public interface ICrashReportService
{
    bool HasPendingCrashReport { get; }
    CrashReportSummary? PendingCrashReport { get; }

    CrashTelemetryConsent TelemetryConsent { get; }
    void SetTelemetryConsent(CrashTelemetryConsent consent);

    void MarkLaunchStarted();
    void MarkLaunchCompleted();

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
