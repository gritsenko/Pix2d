using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Messages;

namespace Pix2d.Abstract.State;

public abstract class StateBase : ObservableObject
{
    private readonly Dictionary<string, List<Action>> _propertyWatchers = new();

    private void OnStateChanged(string propertyName)
    {
        if (_propertyWatchers.TryGetValue(propertyName, out var actions))
            actions.ForEach(x => x.Invoke());

        Messenger.Default.Send(new StateChangedMessage(propertyName));
    }

    internal void AddWatcher(string propertyName, Action onStatePropertyChanged)
    {
        if (!_propertyWatchers.TryGetValue(propertyName, out var actions))
        {
            actions = new List<Action>();
            _propertyWatchers[propertyName] = actions;
        }

        if (!actions.Contains(onStatePropertyChanged))
            actions.Add(onStatePropertyChanged);
    }

    internal void RemoveWatcher(string propertyName, Action onStatePropertyChanged)
    {
        if (!_propertyWatchers.TryGetValue(propertyName, out var actions))
        {
            actions = new List<Action>();
            _propertyWatchers[propertyName] = actions;
        }

        if (actions.Contains(onStatePropertyChanged))
            actions.Remove(onStatePropertyChanged);
    }

    protected override bool Set<T>(T newValue, bool forceNotifyPropertyChanged = false, [CallerMemberName] string propertyName = null)
    {
        var result = base.Set(newValue, forceNotifyPropertyChanged, propertyName);
        OnStateChanged(propertyName);
        return result;
    }
}