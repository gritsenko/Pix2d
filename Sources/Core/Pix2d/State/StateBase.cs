using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract.State;
using Pix2d.Messages;

namespace Pix2d.State;

public abstract class StateBase
{
    private readonly Dictionary<string, List<Action>> _propertyWatchers = new();

    internal void OnStateChanged(string propertyName)
    {
        if (_propertyWatchers.TryGetValue(propertyName, out var actions)) 
            actions.ForEach(x => x.Invoke());

        Messenger.Default.Send(new AppStateChangedMessage(propertyName));
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
}

public static class StateExtensions
{
    public static async void SetAsync<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter, TValue value)
        where TState : IStateBase
    {
        await Task.Run(()=>state.Set(propertyGetter, value));
    }

    public static void Set<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter, TValue value) 
        where TState : IStateBase
    {
        var expression = (MemberExpression)propertyGetter.Body;
        var propName = expression.Member.Name;

        var pInfo = state.GetType().GetProperty(propName);
        if (pInfo == null)
            throw new NullReferenceException($"Property {propName} not found in {state.GetType().Name} type");
        
        pInfo.SetMethod.Invoke(state, new object[] {value});

        if (state is StateBase bState) 
            bState.OnStateChanged(propName);
    }

    /// <summary>
    /// Execute custom setting action then raise state changed event to watchers
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="state"></param>
    /// <param name="propertyGetter"></param>
    /// <param name="setterAction"></param>
    public static void Set<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter, Action setterAction)
        where TState : IStateBase
    {
        var expression = (MemberExpression)propertyGetter.Body;
        var propName = expression.Member.Name;

        setterAction.Invoke();

        if (state is StateBase bState)
            bState.OnStateChanged(propName);
    }

    public static void WatchFor<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter,
        Action onStatePropertyChanged)
    {
        var expression = (MemberExpression)propertyGetter.Body;
        var propName = expression.Member.Name;

        if (state is StateBase bState && onStatePropertyChanged != null)
        {
            bState.AddWatcher(propName, onStatePropertyChanged);
        }
    }
    public static void Unwatch<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter,
        Action onStatePropertyChanged)
    {
        var expression = (MemberExpression)propertyGetter.Body;
        var propName = expression.Member.Name;

        if (state is StateBase bState && onStatePropertyChanged != null)
        {
            bState.RemoveWatcher(propName, onStatePropertyChanged);
        }
    }

}