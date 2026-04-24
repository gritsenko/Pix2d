using Pix2d.Messages;
using Pix2d.Messages.ViewPort;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services;

public class ViewPortService : IViewPortService
{
    private ViewPort? _viewPort;

    private readonly Timer _viewPortChangeTimer;

    private readonly IMessenger _messenger;

    private readonly AppState _state;

    public ViewPort ViewPort
    {
        get => _viewPort!;
        private set
        {
            if (_viewPort != null)
                _viewPort.ViewChanged -= ViewPortOnViewChanged;

            _viewPort = value;

            if (_viewPort != null) 
                _viewPort.ViewChanged += ViewPortOnViewChanged;
        }
    }

    public void Initialize(ViewPort viewPort)
    {
        ViewPort = viewPort;
        ViewPort.ContentBoundsProvider = GetSceneBounds;
        ViewPort.MinVisibleContentPixels = 50;
        SkiaNodes.AdornerLayer.Initialize(this);
        OnViewPortInitialized();
    }

    public ViewPortService(IMessenger messenger, AppState state)
    {
        _messenger = messenger;
        _state = state;
        _viewPortChangeTimer = new Timer(OnViewportTimerTick, null, -1, -1);
        _messenger.Register<ProjectLoadedMessage>(this, _ => EnsureContentVisibility());
        _messenger.Register<OperationInvokedMessage>(this, _ => EnsureContentVisibility());
    }

    private void OnViewportTimerTick(object? state)
    {
        _messenger.Send(ViewPortChangedViewMessage.Default);
    }


    private void ViewPortOnViewChanged(object? sender, EventArgs e)
    {
        _viewPortChangeTimer.Change(300, -1);
    }

    private void OnViewPortInitialized()
    {
        _messenger.Send(ViewPortInitializedMessage.Default);
    }

    public void ShowAll()
    {
        var scene = _state.CurrentProject.SceneNode;
        var viewPort = _viewPort;
        if (scene == null || viewPort == null) return;

        var bBox = scene.GetBoundingBoxWithContent();
        var vpBBox = viewPort.Size;
        viewPort.ShowArea(bBox, new SKSize(vpBBox.Width / 3, vpBBox.Height / 3));
        viewPort.Refresh();
    }

    private SKRect? GetSceneBounds()
    {
        var scene = _state.CurrentProject.SceneNode;
        if (scene == null)
            return null;

        return scene.GetBoundingBoxWithContent();
    }

    private void EnsureContentVisibility()
    {
        ViewPort?.ClampPanToVisibleContent();
    }
}