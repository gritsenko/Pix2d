#nullable enable
using System;
using System.IO;
using System.Text;

namespace Pix2d.Logging;

public class LocalTextFileLoggerTarget : ILoggerTarget
{
    public bool EventsOnly => false;
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LocalTextFileLoggerTarget(string fileName = "pix2d_log.txt")
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(folder, "Pix2dLogs");
        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);
        _logFilePath = Path.Combine(appFolder, fileName);
    }

    public void OnLogged(LogEntry logEntry)
    {
        var sb = new StringBuilder();
        sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logEntry.Level}] {logEntry.Message}");
        if (logEntry.Exception != null)
        {
            sb.Append($"\nException: {logEntry.Exception.Message}\n{logEntry.Exception.StackTrace}");
        }
        if (logEntry.ExtraParams != null)
        {
            foreach (var param in logEntry.ExtraParams)
            {
                sb.Append($"\n{param.Key}: {param.Value}");
            }
        }
        sb.AppendLine();
        lock (_lock)
        {
            File.AppendAllText(_logFilePath, sb.ToString());
        }
    }
}
