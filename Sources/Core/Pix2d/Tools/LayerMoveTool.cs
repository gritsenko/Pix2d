using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Tools;

public class LayerMoveTool : BaseTool
{
    private SKPoint _startPos;
    private SKPoint _endPos;
    private SKPoint _delta;
    private SKPoint _initialPos;

    protected IEditService EditService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IEditService>();
    protected ISelectionService SelectionService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISelectionService>();

    public override Task Activate()
    {
        //todo: make sure edit service is initialized
        var editService = EditService;
        EditService.ShowNodeEditor();
        return base.Activate();
    }

    public override void Deactivate()
    {
        EditService.HideNodeEditor();
        base.Deactivate();
    }

    protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
    {
        _startPos = e.Pointer.WorldPosition;

        _initialPos = SelectionService.Selection.Nodes[0].Position;
    }

    protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
    {
        _endPos = e.Pointer.WorldPosition;

        UpdateDelta();

        _startPos = SKPoint.Empty;

        //ReleasePointerCapture();
        _startPos = SKPoint.Empty;
        _endPos = SKPoint.Empty;

        if (SelectionService.Selection.Nodes[0] is BitmapNode sn)
        {
            //sn.UpdateSizeToParentArtboard();
            //DrawingService.SetDrawingTarget(DrawingService.CurrentDrawingTarget as SpriteNode);
        }

        base.OnPointerReleased(sender, e);
    }

    private void UpdateDelta()
    {
        _delta = new SKPoint(_endPos.X - _startPos.X, _endPos.Y - _startPos.Y);
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        if (e.Pointer.IsPressed)
        {
            _endPos = e.Pointer.WorldPosition;
            UpdateDelta();

            var newPos = _initialPos + _delta;
            SelectionService.Selection.Nodes[0].Position = newPos.SnapToGrid(1);
        }
    }

    protected override void OnPointerDoubleClicked(object sender, PointerActionEventArgs e)
    {
        EditService.RequestEdit(SelectionService.Selection.Nodes);
    }
}