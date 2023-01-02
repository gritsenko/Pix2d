using System;
using System.Reflection;

namespace Mvvm
{
    public class RelayCommand<T> : IRelayCommand
    {
        public string LogEventName;
        private readonly WeakAction<T> _execute;
        private readonly WeakFunc<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            _execute = new WeakAction<T>(execute);

            if (canExecute != null)
            {
                _canExecute = new WeakFunc<T, bool>(canExecute);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            if (parameter == null && typeof(T).GetTypeInfo().IsValueType)
            {
                return _canExecute.Execute(default(T));
            }

            return _canExecute.Execute((T)parameter);
        }

        public virtual void Execute(object parameter)
        {
            var val = parameter;

            //if (!string.IsNullOrEmpty(LogEventName))
            //{
            //    Logger.Log("Command: " + LogEventName);
            //}

            if (CanExecute(val))
            {
                if (val == null && typeof(T).GetTypeInfo().IsValueType)
                {
                    _execute.Execute(default(T));
                }
                else
                {
                    _execute.Execute((T)val);
                }
            }
        }
    }
}