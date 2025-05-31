#nullable enable
namespace Pix2d.Logging;

public class AppStatLoggerTarget : ILoggerTarget
{
    private bool _initialized;
    public bool EventsOnly => false;
    
    public void OnLogged(LogEntry logEntry)
    {
        if (!_initialized)
        {
            _initialized = true;
            Initialize();
        }

        if (logEntry.Exception == null)
        {
            logEntry.IsEvent = true;
        }
    }

    private void Initialize()
    {
    }
}