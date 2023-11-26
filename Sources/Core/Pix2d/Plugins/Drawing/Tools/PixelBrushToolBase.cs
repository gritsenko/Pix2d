using System.Diagnostics;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using SkiaNodes;
using SkiaNodes.Interactive;

namespace Pix2d.Plugins.Drawing.Tools;

public abstract class PixelBrushToolBase : BaseTool, IDrawingTool
{
    protected IDrawingService DrawingService => CoreServices.DrawingService;
    protected ISelectionService SelectionService => CoreServices.SelectionService;
    
    private SKNode _drawingLayerNode;
    private BrushDrawingMode _drawingMode = BrushDrawingMode.Draw;

    public virtual BrushDrawingMode DrawingMode
    {
        get => _drawingMode;
        protected set => _drawingMode = value;
    }

    public override async Task Activate()
    {
        await base.Activate();
        try
        {
            _drawingLayerNode = (SKNode)DrawingService.DrawingLayer;
            _drawingLayerNode.PointerPressed += DrawingLayerNode_PointerPressed;
            DrawingService.DrawingTargetChanged += DrawingService_DrawingTargetChanged;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            throw;
        }
    }


    public override void Deactivate()
    {
        base.Deactivate();

        _drawingLayerNode.PointerPressed -= DrawingLayerNode_PointerPressed;
        DrawingService.DrawingTargetChanged -= DrawingService_DrawingTargetChanged;

        DrawingService.DrawingLayer.ShowBrushPreview = false;
    }

    private void DrawingService_DrawingTargetChanged(object sender, EventArgs e)
    {
        _drawingLayerNode = (SKNode)DrawingService.DrawingLayer;
        DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
    }

    private void DrawingLayerNode_PointerPressed(object sender, PointerActionEventArgs e)
    {
        _drawingMode = !e.Pointer.IsEraser ? BrushDrawingMode.Draw : BrushDrawingMode.Erase;
        DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        DrawingService.DrawingLayer.ShowBrushPreview = !e.Pointer.IsTouch;

        //CHECK IF WE STILL ON OLD SPRITE
        if (!e.Pointer.IsPressed && !_drawingLayerNode.ContainsPoint(e.Pointer.WorldPosition))
        {
            var container = SelectionService.GetContainer(e.Pointer.WorldPosition);
            if (container is IDrawingTarget dt)
                DrawingService.SetDrawingTarget(dt);
        }

        if (DrawingService.DrawingLayer.ShowBrushPreview)
        {
            DrawingService.Refresh();
        }
    }

    protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifier.Alt) == 0) return;
        
        DrawingService.PickColorByPoint(e.Pointer.WorldPosition);
        e.Handled = true;
    }
}