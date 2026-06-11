using System.Linq.Expressions;
using Pix2d.State;

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

    /// <summary>
    /// Watches a property of the CURRENT project, re-binding automatically when
    /// <see cref="AppState.CurrentProject"/> is replaced (project tab switch). A plain
    /// CurrentProject.WatchFor(...) binds to the ProjectState instance captured at subscription
    /// time and silently goes stale after a switch. The callback is also invoked right after a
    /// re-bind so subscribers resync to the new project's values.
    /// </summary>
    public static void WatchForCurrentProject<TValue>(this AppState appState,
        Expression<Func<ProjectState, TValue>> propertyGetter, Action onStatePropertyChanged)
    {
        RebindOnProjectSwitch(appState, p => p, propertyGetter, onStatePropertyChanged);
    }

    /// <summary>
    /// Same as <see cref="WatchForCurrentProject{TValue}"/> but for the per-project
    /// <see cref="ProjectState.ViewPortState"/> sub-state.
    /// </summary>
    public static void WatchForCurrentProjectViewPort<TValue>(this AppState appState,
        Expression<Func<ViewPortState, TValue>> propertyGetter, Action onStatePropertyChanged)
    {
        RebindOnProjectSwitch(appState, p => p.ViewPortState, propertyGetter, onStatePropertyChanged);
    }

    private static void RebindOnProjectSwitch<TState, TValue>(AppState appState,
        Func<ProjectState, TState> subStateGetter,
        Expression<Func<TState, TValue>> propertyGetter,
        Action onStatePropertyChanged) where TState : StateBase
    {
        var watched = subStateGetter(appState.CurrentProject);
        watched.WatchFor(propertyGetter, onStatePropertyChanged);

        appState.WatchFor(x => x.CurrentProject, () =>
        {
            var fresh = subStateGetter(appState.CurrentProject);
            if (ReferenceEquals(fresh, watched))
                return;

            watched.Unwatch(propertyGetter, onStatePropertyChanged);
            watched = fresh;
            watched.WatchFor(propertyGetter, onStatePropertyChanged);
            onStatePropertyChanged();
        });
    }
}