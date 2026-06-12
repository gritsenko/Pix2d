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

    public SnappingService(ISceneService sceneService, IMessenger messenger, AppState appState)
    {
        SceneService = sceneService;
        Messenger = messenger;
        AppState = appState;

        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
        // Re-binding watchers: a project switch (tab) re-applies the new project's grid without
        // zeroing it — only a fresh load (ProjectLoadedMessage) resets ShowGrid.
        AppState.WatchForCurrentProjectViewPort(x => x.ShowGrid, UpdateContainersGrids);
        AppState.WatchForCurrentProjectViewPort(x => x.GridSpacing, UpdateContainersGrids);
    }

    private void OnProjectLoaded(ProjectLoadedMessage obj)
    {
        AppState.CurrentProject.ViewPortState.ShowGrid = false;
    }

    private void UpdateContainersGrids()
    {
        var containerBaseNodes =
            SceneService.GetCurrentSceneContainers<DrawingContainerBaseNode>()?.ToArray() ??
            [];

        foreach (var containerBaseNode in containerBaseNodes)
        {
            containerBaseNode.GridCellSize = AppState.CurrentProject.ViewPortState.GridSpacing;
            containerBaseNode.ShowGrid = AppState.CurrentProject.ViewPortState.ShowGrid;
        }
    }
}