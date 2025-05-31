#nullable enable
using System.Runtime.CompilerServices;
using System.Text;

namespace Pix2d;

public class Logger
{
    private static readonly Logger _instance = new();

    private readonly List<ILoggerTarget> _targets = new();

    private readonly Dictionary<string, string> _paramDict = new();


    public static void RegisterLoggerTarget(ILoggerTarget target)
    {
        _instance._targets.Add(target);
    }

    public static void LogException(Exception ex, [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerMemberName = null)
    {
        _instance.AddLogEntry(ex, $"Exception in {callerFilePath}.{callerMemberName} ");
    }

    public static void Log(string message, params object[] args)
    {
        _instance.AddLogEntry(null, message, args);
    }

    public static void Trace(string message, [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerMemberName = null)
    {
        var msg = Path.GetFileName(callerFilePath?.Replace(".cs", "") ?? "") + "." + callerMemberName + ": " + message;
        
        var entry = new LogEntry(msg)
        {
            Level = LogLevel.Trace
        };

        foreach (var loggerTarget in _instance._targets)
            loggerTarget.OnLogged(entry);
    }

    private void AddLogEntry(Exception ex, string message, object[]? args = default, string eventId = "unidentified_event")
    {
        var entry = new LogEntry(message, args)
        {
            Exception = ex,
            EventId = eventId,
            Level = ex != null ? LogLevel.Error : LogLevel.Info
        };

        entry.ExtraParams = entry.ExtraParams == null ? _paramDict : entry.ExtraParams.Concat(_paramDict).ToDictionary(x => x.Key, x => x.Value);

        foreach (var loggerTarget in _targets) 
            loggerTarget.OnLogged(entry);
    }

    public static void LogEventWithParams(string eventName, IDictionary<string, string?>? extraParams, IDictionary<string, double>? metrics = null)
    {
        var entry = new LogEntry(eventName) { IsEvent = true, ExtraParams = extraParams, Metrics = metrics };

        foreach (var loggerTarget in _instance._targets)
            loggerTarget.OnLogged(entry);
    }


}

public class LogEntry
{
    public DateTime Time { get; set; }
    public string Message { get; set; }
    public string EventId { get; set; }
    public Exception? Exception { get; set; }
    public bool IsEvent { get; set; }
    public IDictionary<string, string>? ExtraParams { get; set; }
    public IDictionary<string, double>? Metrics { get; set; }

    public LogLevel Level { get; set; }
    public LogEntry(string message, params object[] args)
    {
        Time = DateTime.Now;

        if (args == null || args.Length == 0)
        {
            Message = message;
            return;
        }

        Message = string.Format(message, args);
    }

    public override string ToString()
    {
        var sb = new StringBuilder()
            .Append(Time.ToString("t"))
            .Append(": ")
            .Append(Message);

        if (Exception != null)
        {
            sb.AppendLine("exception:")
                .AppendLine(Exception.Message)
                .AppendLine(Exception.StackTrace);
        }
        return sb.ToString();
    }
}

public enum LogLevel
{
    Trace,
    Info,
    Error,
}