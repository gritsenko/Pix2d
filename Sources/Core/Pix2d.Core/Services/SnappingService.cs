using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using SkiaNodes.Interactive;

namespace Pix2d.Services;

public class SnappingService : ISnappingService
{
    public ISceneService SceneService { get; }
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    public bool IsAspectLocked => ForceAspectLock || SKInput.Current.GetModifiers().HasFlag(KeyModifier.Shift);

    public bool ForceAspectLock { get; set; }
    public bool DrawFromCenterLocked => ForceDrawFromCenterAspectLock || SKInput.Current.GetModifiers().HasFlag(KeyModifier.Ctrl);
    public bool ForceDrawFromCenterAspectLock { get; set; }

    public SnappingService(ISceneService sceneService, IMessenger messenger, AppState appState)
    {
        SceneService = sceneService;
        Messenger = messenger;
        AppState = appState;

        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
        Messenger.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<ViewPortState>(x => x.ShowGrid, UpdateContainersGrids));
        Messenger.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<ViewPortState>(x => x.GridSpacing, UpdateContainersGrids));
    }

    private void OnProjectLoaded(ProjectLoadedMessage obj)
    {
        AppState.CurrentProject.ViewPortState.ShowGrid = false;
    }

    private void UpdateContainersGrids()
    {
        var containerBaseNodes = SceneService.GetCurrentSceneContainers<DrawingContainerBaseNode>().ToArray();

        foreach (var containerBaseNode in containerBaseNodes)
        {
            containerBaseNode.GridCellSize = AppState.CurrentProject.ViewPortState.GridSpacing;
            containerBaseNode.ShowGrid = AppState.CurrentProject.ViewPortState.ShowGrid;
        }
    }
}