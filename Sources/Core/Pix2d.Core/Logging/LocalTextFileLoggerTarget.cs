#nullable enable
using System;
using System.IO;
using System.Text;

namespace Pix2d.Logging;

public class LocalTextFileLoggerTarget : ILoggerTarget
{
    /// <summary>
    /// The log is append-only for the lifetime of an install, so without a cap it grows forever and
    /// eventually becomes the app's own contribution to a full disk (appstat, 3.11.3: writing
    /// pix2d_log.txt threw <see cref="IOException"/> "not enough space on the disk"). At the cap the
    /// file is started over rather than rotated — keeping a backup would double the footprint, which
    /// is the opposite of what a disk-pressure guard should do.
    /// </summary>
    private const long MaxLogFileBytes = 5 * 1024 * 1024;

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
            try
            {
                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > MaxLogFileBytes)
                    File.WriteAllText(_logFilePath, sb.ToString());
                else
                    File.AppendAllText(_logFilePath, sb.ToString());
            }
            catch (Exception)
            {
                // Losing a log line is never worth an exception: OnLogged usually runs while an error is
                // already being reported. Logger.Dispatch also guards, this keeps the failure from
                // stopping the remaining targets in older/other dispatch paths.
            }
        }
    }
}
