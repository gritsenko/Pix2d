namespace Pix2d
{
    public interface ILoggerTarget
    {
        bool EventsOnly { get; }

        void OnLogged(LogEntry logEntry);
    }
}
