using System.Runtime.CompilerServices;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Messages;

namespace Pix2d.Abstract.State;

public abstract class StateBase : ObservableObject
{
    private readonly Dictionary<string, List<Action>> _propertyWatchers = new();
    private readonly List<Action> _globalWatchers = new();

    private void OnStateChanged(string propertyName)
    {
        if (_propertyWatchers.TryGetValue(propertyName, out var actions))
            actions.ForEach(x => x.Invoke());

        // Invoke global watchers
        _globalWatchers.ForEach(x => x.Invoke());
    }

    /// <summary>
    /// Adds a watcher for a specific property.
    /// </summary>
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

    /// <summary>
    /// Removes a watcher for a specific property.
    /// </summary>
    internal void RemoveWatcher(string propertyName, Action onStatePropertyChanged)
    {
        if (!_propertyWatchers.TryGetValue(propertyName, out var actions))
        {
            actions = [];
            _propertyWatchers[propertyName] = actions;
        }

        if (actions.Contains(onStatePropertyChanged))
            actions.Remove(onStatePropertyChanged);
    }

    /// <summary>
    /// Adds a watcher that triggers when any property changes.
    /// </summary>
    internal void AddGlobalWatcher(Action onAnyPropertyChanged)
    {
        if (!_globalWatchers.Contains(onAnyPropertyChanged))
            _globalWatchers.Add(onAnyPropertyChanged);
    }

    /// <summary>
    /// Removes a watcher that triggers when any property changes.
    /// </summary>
    internal void RemoveGlobalWatcher(Action onAnyPropertyChanged)
    {
        if (_globalWatchers.Contains(onAnyPropertyChanged))
            _globalWatchers.Remove(onAnyPropertyChanged);
    }

    protected override bool Set<T>(T newValue, bool forceNotifyPropertyChanged = false, [CallerMemberName] string? propertyName = null)
    {
        var result = base.Set(newValue, forceNotifyPropertyChanged, propertyName);
        OnStateChanged(propertyName);
        return result;
    }
}