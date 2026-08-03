using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using SkiaNodes.Interactive;

namespace Pix2d.Services;

public class SnappingService : ISnappingService
{
    private ISceneService SceneService { get; }
    private IMessenger Messenger { get; }
    private AppState AppState { get; }
    public bool IsAspectLocked => ForceAspectLock || SKInput.Current.GetModifiers().HasFlag(KeyModifier.Shift);

    public bool ForceAspectLock { get; set; }

    public bool DrawFromCenterLocked =>
        ForceDrawFromCenterAspectLock || SKInput.Current.GetModifiers().HasFlag(KeyModifier.Ctrl);

    public bool ForceDrawFromCenterAspectLock { get; set; }

    private IViewPortRefreshService ViewPortRefreshService { get; }

    public SnappingService(ISceneService sceneService, IMessenger messenger, AppState appState,
        IViewPortRefreshService viewPortRefreshService)
    {
        SceneService = sceneService;
        Messenger = messenger;
        AppState = appState;
        ViewPortRefreshService = viewPortRefreshService;

        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
        // Re-binding watchers: a project switch (tab) re-applies the new project's grid without
        // zeroing it — only a fresh load (ProjectLoadedMessage) resets ShowGrid.
        AppState.WatchForCurrentProjectViewPort(x => x.ShowGrid, UpdateContainersGrids);
        AppState.WatchForCurrentProjectViewPort(x => x.GridSpacing, UpdateContainersGrids);
        // Grid color is an app-wide preference (#223), not per-project — watch it on AppState directly.
        AppState.WatchFor(x => x.GridColor, UpdateContainersGrids);
    }

    private void OnProjectLoaded(ProjectLoadedMessage obj)
    {
        AppState.CurrentProject.ViewPortState.ShowGrid = false;
    }

    private void UpdateContainersGrids()
    {
        // Keep the new-node default in step, so an artboard added later starts with the chosen color
        // instead of the built-in gray (its GridNode is created before any watcher can reach it).
        GridDefaults.CurrentColor = AppState.GridColor;

        var containerBaseNodes =
            SceneService.GetCurrentSceneContainers<DrawingContainerBaseNode>()?.ToArray() ??
            [];

        foreach (var containerBaseNode in containerBaseNodes)
        {
            containerBaseNode.GridCellSize = AppState.CurrentProject.ViewPortState.GridSpacing;
            containerBaseNode.GridColor = AppState.GridColor;
            containerBaseNode.ShowGrid = AppState.CurrentProject.ViewPortState.ShowGrid;
        }

        // Repaint from here rather than adding another watcher next to ViewPortRefreshService's: both would
        // fire on the same property in an undefined order, and a refresh that ran BEFORE this push would draw
        // the previous color and leave the new one invisible until something else invalidated the scene —
        // which is exactly the "the color only applies once the flyout closes" lag. Pushing then refreshing
        // makes live scrubbing of the color/opacity land on the canvas immediately.
        ViewPortRefreshService.Refresh();
    }
}