#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using SkiaNodes;

namespace Pix2d.Services.Project;

public class ProjectActivationService : IProjectActivationService
{
    private readonly AppState _appState;
    private readonly IMessenger _messenger;
    private readonly IOperationService _operationService;
    private readonly IViewPortService _viewPortService;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IEditService _editService;
    // SpriteEditor / IDrawingService / tracker are resolved lazily: they are plugin-registered
    // (or registered later in the container) and pulling them through the ctor would couple this
    // service to plugin load order.
    private readonly IServiceProvider _serviceProvider;

    public ProjectActivationService(AppState appState,
        IMessenger messenger,
        IOperationService operationService,
        IViewPortService viewPortService,
        IViewPortRefreshService viewPortRefreshService,
        IEditService editService,
        IServiceProvider serviceProvider)
    {
        _appState = appState;
        _messenger = messenger;
        _operationService = operationService;
        _viewPortService = viewPortService;
        _viewPortRefreshService = viewPortRefreshService;
        _editService = editService;
        _serviceProvider = serviceProvider;
    }

    public void ActivateProject(ProjectState target)
    {
        if (target == null || ReferenceEquals(_appState.CurrentProject, target))
            return;

        // A tab always owns a loaded scene; without one there is nothing to show.
        if (target.SceneNode == null)
            return;

        DeactivateCurrentAndMakeCurrent(target);

        // Swap the scene WITHOUT ProjectLoadedMessage — that message means "fresh load" and would
        // clear the undo history, reset the grid and re-frame the view. SceneCreated re-writes
        // CurrentProject.SceneNode, which is why CurrentProject must already point at the target.
        SKApp.SceneManager.SetScene(target.SceneNode);

        // Re-target the editor (mirrors EditService.OnProjectLoadedMessage): cycling
        // CurrentNodeEditor reloads the Layers/Timeline panels and re-points the shared
        // drawing layer at the target's active artboard.
        var sprite = target.CurrentEditedNode as Pix2dSprite
                     ?? target.SceneNode.Nodes.OfType<Pix2dSprite>().FirstOrDefault();
        if (sprite != null)
            _editService.RequestEdit([sprite]);

        RestoreViewPort(target);

        _messenger.Send(new ProjectActivatedMessage(target));
        _viewPortRefreshService.Refresh();
    }

    public void BeginNewProjectActivation(ProjectState newProject)
    {
        if (newProject == null || ReferenceEquals(_appState.CurrentProject, newProject))
            return;

        DeactivateCurrentAndMakeCurrent(newProject);
    }

    public void MoveProjectToFrontAndActivate(ProjectState target)
    {
        if (target == null)
            return;

        var projects = _appState.LoadedProjects;
        var index = projects.IndexOf(target);
        if (index < 0)
            return;

        if (index > 0)
        {
            projects.RemoveAt(index);
            projects.Insert(0, target);

            // ActivateProject re-derives the index, but it returns early when the target is already
            // current (reorder without a switch), so the invariant is restored here as well.
            var currentIndex = projects.IndexOf(_appState.CurrentProject);
            if (currentIndex >= 0)
                _appState.ActiveProjectIndex = currentIndex;

            // Rebuild the tab strip BEFORE activating, so the selection sync that follows
            // activation sees the new order.
            _messenger.Send(ProjectsListChangedMessage.Default);
        }

        ActivateProject(target);
    }

    private void DeactivateCurrentAndMakeCurrent(ProjectState target)
    {
        SaveCurrentViewPortState();

        _serviceProvider.GetService<SpriteEditor>()?.Stop();
        _serviceProvider.GetService<IDrawingService>()?.CancelCurrentOperation();

        // No autosave-tracker work here: dirty cells are bucketed per project, so the
        // outgoing project's pending changes stay parked in its bucket and are committed
        // into its own session store on the next autosave tick.

        _operationService.SetActiveHistory(target.Id);

        var index = _appState.LoadedProjects.IndexOf(target);
        if (index >= 0)
            _appState.ActiveProjectIndex = index;

        // Fires the WatchFor(x => x.CurrentProject) re-binds (panels, snapping, tools).
        _appState.CurrentProject = target;
    }

    private void SaveCurrentViewPortState()
    {
        var viewPort = _viewPortService.ViewPort;
        if (viewPort == null)
            return;

        var state = _appState.CurrentProject.ViewPortState;
        state.Zoom = viewPort.Zoom;
        state.Pan = viewPort.Pan;
    }

    private void RestoreViewPort(ProjectState target)
    {
        var viewPort = _viewPortService.ViewPort;
        var state = target.ViewPortState;

        // Zoom == 0 means the project was never framed — fall back to fit-all.
        if (viewPort != null && state.Zoom > 0)
        {
            viewPort.SetZoom(state.Zoom);
            viewPort.SetPan(state.Pan.X, state.Pan.Y);
        }
        else
        {
            _viewPortService.ShowAll();
        }
    }
}
