using Pix2d.Abstract.Drawing;
using SkiaNodes;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

public abstract class PixelShapeTool<TShapeBuilder>(
    IMessenger messenger,
    IDrawingService drawingService,
    ISelectionService selectionService,
    IViewPortRefreshService viewPortRefreshService)
    : PixelBrushToolBase(messenger, drawingService, selectionService)
    where TShapeBuilder : ShapeBuilderBase, new()
{
    private ShapeBuilderBase? _currentBuilder;
    private bool _isDrawing;

    public override BrushDrawingMode DrawingMode => BrushDrawingMode.ExternalDraw;

    public override Task Activate()
    {
        _currentBuilder ??= GetShapeBuilder();

        var result = base.Activate();

        UpdateDrawingMode();

        return result;
    }

    protected virtual ShapeBuilderBase GetShapeBuilder()
    {
        var builder = new TShapeBuilder();
        builder.Initialize(DrawingService.DrawingLayer);
        return builder;
    }

    protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
    {
        base.OnPointerPressed(sender, e);

        if (!e.Handled)
        {
            //drawing shapes
            _currentBuilder.Reset();
            _currentBuilder.BeginDrawing();
            DrawingService.DrawingLayer.UseSwapBitmap = true;
            _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
            _isDrawing = true;
        }
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        base.OnPointerMoved(sender, e);

        if (_isDrawing && e.Pointer.IsPressed && _currentBuilder?.AddPointInputMode == AddPointInputMode.PressAndHold)
        {
            _currentBuilder?.SetNextPointPreview(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
            DrawingService.DrawingLayer.FinishCurrentDrawing();
            viewPortRefreshService.Refresh();
        }
    }

    protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
    {
        if (_isDrawing && _currentBuilder.AddPointInputMode == AddPointInputMode.PressAndHold)
        {
            _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
        }

        _isDrawing = false;
    }

    private void UpdateDrawingMode()
    {
        DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
    }
}