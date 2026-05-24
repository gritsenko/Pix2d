using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Primitives;

namespace Pix2d;

public class EnableOnClipboardSelectionCommandBehavior : ICommandBehaviour
{
    private readonly AppState _appState;
    private readonly IDrawingService _drawingService;
    private readonly List<Pix2dCommand> _commands = [];

    public EnableOnClipboardSelectionCommandBehavior(AppState appState, IDrawingService drawingService, IMessenger messenger)
    {
        _appState = appState;
        _drawingService = drawingService;

        _drawingService.DrawingLayer.PixelsSelected += OnSelectionStateChanged;
        _drawingService.DrawingLayer.SelectionRemoved += OnSelectionStateChanged;

        _appState.ToolsState.WatchFor(x => x.CurrentToolKey, UpdateCommands);
        _appState.WatchFor(x => x.CurrentProject, UpdateCommands);

        messenger.Register<NodesSelectedMessage>(this, _ => UpdateCommands());
    }

    public void Attach(Pix2dCommand command)
    {
        _commands.Add(command);
        command.SetCanExecute(CanExecute());
    }

    private void OnSelectionStateChanged(object? sender, EventArgs e)
    {
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        var canExecute = CanExecute();
        foreach (var command in _commands)
            command.SetCanExecute(canExecute);
    }

    private bool CanExecute()
    {
        if (_appState.ToolsState.CurrentTool?.ToolInstance is not IPixelSelectionTool)
            return false;

        return _drawingService.DrawingLayer.HasSelection || _appState.CurrentProject.HasSelection;
    }
}