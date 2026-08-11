#nullable enable
namespace Pix2d.Primitives.Crash;

public sealed class CrashReportSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string ExceptionChain { get; set; } = string.Empty;
    public string SessionOperationLog { get; set; } = string.Empty;
    public string LogTail { get; set; } = string.Empty;
    public string? StartupDocument { get; set; }
    public string? LastCommandName { get; set; }

    /// <summary>
    /// Compact one-line snapshot of app state at capture time (active tool, open tabs, canvas size,
    /// current frame/layer, selection). Cheap to attach and, unlike the stack, always present — the
    /// single most useful field for triaging a frame-less exception (empty <see cref="StackTrace"/>),
    /// which is common on trimmed/AOT Android builds.
    /// </summary>
    public string? AppContext { get; set; }

    public string Source { get; set; } = string.Empty;
    public bool IsImplicit { get; set; }

    /// <summary>
    /// Explicit telemetry grouping key, set only for crashes reconstructed from an OS exit record
    /// (see <see cref="NativeCrashSignature"/>). Such an event has no managed stack and no real
    /// exception, so neither of Sentry's usual grouping strategies can work on it — the key is
    /// computed here instead. It is persisted with the envelope because the report is written on the
    /// launch that discovers the crash but may only be *sent* on a later one (after consent), by
    /// which time the OS exit record it was derived from is gone.
    /// </summary>
    public string? TelemetryFingerprint { get; set; }

    /// <summary>
    /// How stale the recovered context was: milliseconds between the last session-crumb refresh and
    /// the process death it is attributed to. Near-zero means the op-log/last-command genuinely
    /// describe the crash; a large value means the app sat idle and the context is only indicative.
    /// Null when not applicable or unknown.
    /// </summary>
    public long? ContextAgeMs { get; set; }

    public string FormatForDisplay()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Pix2d crash report");
        sb.AppendLine($"Id: {Id}");
        sb.AppendLine($"Time: {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"App: {AppVersion} ({Platform})");
        sb.AppendLine($"Source: {Source}");
        if (IsImplicit)
            sb.AppendLine("Note: previous launch did not finish — this report is reconstructed from session state.");
        if (!string.IsNullOrEmpty(StartupDocument))
            sb.AppendLine($"Startup document: {StartupDocument}");
        if (!string.IsNullOrEmpty(LastCommandName))
            sb.AppendLine($"Last command: {LastCommandName}");
        if (!string.IsNullOrEmpty(AppContext))
            sb.AppendLine($"App context: {AppContext}");

        sb.AppendLine();
        sb.AppendLine("=== Exception ===");
        sb.AppendLine($"{ExceptionType}: {Message}");
        if (!string.IsNullOrEmpty(StackTrace))
        {
            sb.AppendLine();
            sb.AppendLine(StackTrace);
        }
        if (!string.IsNullOrEmpty(ExceptionChain))
        {
            sb.AppendLine();
            sb.AppendLine("=== Exception chain ===");
            sb.AppendLine(ExceptionChain);
        }

        if (!string.IsNullOrEmpty(SessionOperationLog))
        {
            sb.AppendLine();
            sb.AppendLine("=== Session operations ===");
            sb.AppendLine(SessionOperationLog);
        }

        if (!string.IsNullOrEmpty(LogTail))
        {
            sb.AppendLine();
            sb.AppendLine("=== Recent log ===");
            sb.AppendLine(LogTail);
        }

        return sb.ToString();
    }
}
