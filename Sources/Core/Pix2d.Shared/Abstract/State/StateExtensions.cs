using System.Linq.Expressions;

namespace Pix2d.Abstract.State;

public static class StateExtensions
{
    public static void Watch<TState>(this TState state, Action onStatePropertyChanged) where TState : StateBase
    {
        state.AddGlobalWatcher(onStatePropertyChanged);
    }
    public static void Unwatch<TState>(this TState state, Action onStatePropertyChanged) where TState : StateBase
    {
        state.AddGlobalWatcher(onStatePropertyChanged);
    }

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