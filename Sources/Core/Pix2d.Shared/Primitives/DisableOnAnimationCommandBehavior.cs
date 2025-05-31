#nullable enable
using Pix2d.State;

namespace Pix2d.Primitives;

public class DisableOnAnimationCommandBehavior : ICommandBehaviour
{
    private readonly AppState _appState;
    private readonly List<Pix2dCommand> _commands = [];

    public DisableOnAnimationCommandBehavior(AppState appState)
    {
        _appState = appState;
        _appState.CurrentProject.WatchFor(x => x.IsAnimationPlaying, OnAnimationPlayingChanged);
    }

    private void OnAnimationPlayingChanged()
    {
        var canExecute = !_appState.CurrentProject.IsAnimationPlaying;
        foreach (var command in _commands) 
            command.SetCanExecute(canExecute);
    }

    public void Attach(Pix2dCommand command)
    {
        _commands.Add(command);
    }
}