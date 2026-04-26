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
    public string Source { get; set; } = string.Empty;
    public bool IsImplicit { get; set; }

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
