using Pix2d.Abstract.Tools;
using Pix2d.InteractiveNodes;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Tools;

public abstract class ObjectCreationTool(
    IObjectCreationService objectCreationService,
    ISceneService sceneService,
    IEditService editService) : BaseTool
{
    private Frame _selectionFrame = new Frame();
    private SKNode _scene;
    
    public override Task Activate()
    {
        _scene = sceneService.GetCurrentScene();
        var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(_scene);
        adornerLayer.Add(_selectionFrame);
        _selectionFrame.IsVisible = false;
        return base.Activate();
    }

    public override void Deactivate()
    {
        editService.HideNodeEditor();
        base.Deactivate();
    }

    protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
    {
        _selectionFrame.Position = e.Pointer.WorldPosition;
        _selectionFrame.SetSecondCornerPosition(_selectionFrame.Position);
        _selectionFrame.IsVisible = true;
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        if (e.Pointer.IsPressed)
        {
            _selectionFrame.SetSecondCornerPosition(e.Pointer.WorldPosition);
        }
    }

    protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
    {
        _selectionFrame.IsVisible = false;

        CreateObjectCore(_selectionFrame.GetBoundingBox());
    }

    protected abstract void CreateObjectCore(SKRect destRect);
}