using System;

namespace Pix2d.Infrastructure.Logger;

public class ConsoleLoggerTarget : ILoggerTarget
{
    public bool EventsOnly => false;

    public void OnLogged(LogEntry logEntry)
    {
        if (logEntry.Exception == null)
        {
            logEntry.IsEvent = true;
        }

        Console.WriteLine(logEntry.Message);
        if (logEntry.Exception != null)
        {
            Console.WriteLine(logEntry.Exception.Message);
            Console.WriteLine(logEntry.Exception.StackTrace);
        }
    }

}