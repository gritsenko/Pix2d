using System;

namespace Mvvm
{
    public class RelayCommand : IRelayCommand
    {
        //private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly WeakAction _execute;
        private readonly WeakFunc<bool> _canExecute;

        public string LogEventName;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            _execute = new WeakAction(execute);

            if (canExecute != null)
            {
                _canExecute = new WeakFunc<bool>(canExecute);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute.Execute();
        }

        public virtual void Execute(object parameter)
        {
            if (!string.IsNullOrEmpty(LogEventName))
            {
                //Logger.Log(LogLevel.Info, "Command: " + LogEventName);
            }

            if (CanExecute(parameter))
            {
                _execute.Execute();
            }
        }
    }
}