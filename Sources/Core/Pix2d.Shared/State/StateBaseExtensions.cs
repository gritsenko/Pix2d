using System;
using System.Linq.Expressions;
using Pix2d.Abstract.State;

namespace Pix2d.State;

public static class StateBaseExtensions
{
    public static void WatchFor<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter,
        Action onStatePropertyChanged) where TState : StateBase
    {
        var expression = (MemberExpression)propertyGetter.Body;
        var propName = expression.Member.Name;

        state.AddWatcher(propName, onStatePropertyChanged);
    }
    public static void Unwatch<TState, TValue>(this TState state, Expression<Func<TState, TValue>> propertyGetter,
        Action onStatePropertyChanged) where TState : StateBase
    {
        var expression = (MemberExpression)propertyGetter.Body;
        var propName = expression.Member.Name;

        state.RemoveWatcher(propName, onStatePropertyChanged);
    }
}