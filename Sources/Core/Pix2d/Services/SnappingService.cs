using System;
using System.Linq;
using Mvvm.Messaging;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Services;

public class SnappingService : ISnappingService
{
    public ISceneService SceneService { get; }
    public IMessenger Messenger { get; }
    private bool _showGrid;
    private SKSize _gridCellSize = new SKSize(8,8);
    public event EventHandler GridToggled;
    public event EventHandler GridCellSizeChanged;
    public bool IsAspectLocked => ForceAspectLock || SKInput.Current.GetModifiers().HasFlag(KeyModifier.Shift);

    public bool ForceAspectLock { get; set; }
    public bool DrawFromCenterLocked => ForceDrawFromCenterAspectLock || SKInput.Current.GetModifiers().HasFlag(KeyModifier.Ctrl);
    public bool ForceDrawFromCenterAspectLock { get; set; }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value; 
            OnGridToggled();
        }
    }

    public SKSize GridCellSize
    {
        get => _gridCellSize;
        set
        {
            var w = value.Width;
            var h = value.Height;
            w = Math.Min(256, Math.Max(1, w));
            h = Math.Min(256, Math.Max(1, h));
            _gridCellSize = new SKSize(w, h);
            OnGridCellSizeChanged();
        }
    }

    public SnappingService(ISceneService sceneService, IMessenger messenger)
    {
        SceneService = sceneService;
        Messenger = messenger;

        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
    }

    private void OnProjectLoaded(ProjectLoadedMessage obj)
    {
        ShowGrid = false;
    }

    protected virtual void OnGridToggled()
    {
        UpdateContainersGrids();
        GridToggled?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateContainersGrids()
    {
        var containerBaseNodes = SceneService.GetCurrentSceneContainers<DrawingContainerBaseNode>().ToArray();

        foreach (var containerBaseNode in containerBaseNodes)
        {
            containerBaseNode.GridCellSize = _gridCellSize;
            containerBaseNode.ShowGrid = _showGrid;
        }
    }

    protected virtual void OnGridCellSizeChanged()
    {
        UpdateContainersGrids();
        GridCellSizeChanged?.Invoke(this, EventArgs.Empty);
    }
}