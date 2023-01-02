using System;

namespace Mvvm
{
    public class LoggedRelayCommand : RelayCommand
    {
        public LoggedRelayCommand(Action action, Func<bool> func, string logEventName)
            : base(action, func)
        {
            LogEventName = logEventName;
        }
    }

    public class LoggedRelayCommand<T> : RelayCommand<T>
    {
        private readonly string _logEventName;

        public LoggedRelayCommand(Action<T> execute, string logEventName = null)
            : base(execute)
        {
            _logEventName = logEventName;
        }

        public LoggedRelayCommand(Action<T> execute, Func<T, bool> canExecute, string logEventName)
            : base(execute, canExecute)
        {
            _logEventName = logEventName;
        }
    }
}