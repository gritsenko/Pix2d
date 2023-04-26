using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mvvm;

[Bindable(true)]
public class ViewModelBase : ObservableObject
{

    public static T GetService<T>()
    {
        return DefaultServiceLocator.ServiceLocatorProvider().GetInstance<T>();
    }

    private readonly Dictionary<string, IRelayCommand> _commandCache = new Dictionary<string, IRelayCommand>();

    protected RelayCommand<T> GetCommand<T>(Action<T> action, string message, Func<T, bool> canExecute = null, [CallerMemberName] string key = null)
    {
        return (RelayCommand<T>)GetCommand(key, () => new LoggedRelayCommand<T>(action, canExecute, message));
    }

    protected RelayCommand<T> GetCommand<T>(Action<T> action, Func<T, bool> canExecute = null, [CallerMemberName] string key = null)
    {
        return (RelayCommand<T>)GetCommand(key, () => new RelayCommand<T>(action, canExecute));
    }

    protected IRelayCommand GetCommand(Action action, string message, Func<bool> canExecute = null, [CallerMemberName] string key = null)
    {
        return GetCommand(key, () => new LoggedRelayCommand(action, canExecute, message));
    }
    protected IRelayCommand GetCommand(Action action, Func<bool> canExecute = null, [CallerMemberName] string key = null)
    {
        return GetCommand(key, () => new RelayCommand(action, canExecute));
    }

    private IRelayCommand GetCommand(string key, Func<IRelayCommand> newCommandBuilder)
    {
        lock (_commandCache)
        {
            IRelayCommand cmd;

            if (!_commandCache.TryGetValue(key, out cmd))
            {
                cmd = newCommandBuilder();
                _commandCache[key] = cmd;

                var rc = cmd as RelayCommand;

                if (rc != null)
                {
                    rc.LogEventName = key;
                }
            }

            return cmd;
        }
    }
}