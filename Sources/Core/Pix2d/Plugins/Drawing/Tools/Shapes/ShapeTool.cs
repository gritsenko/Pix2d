using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Primitives.Drawing;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Drawing.Tools;

[Pix2dTool(EditContextType = EditContextType.Sprite)]
public class ShapeTool : BaseTool, IDrawingTool
{
    public IDrawingService DrawingService { get; }
    private ShapeBuilderBase _currentBuilder;
    private ShapeType _shapeType = ShapeType.Rectangle;
    private SKPoint _lastPoint;

    public event EventHandler ShapeTypeChanged;

    public ShapeType ShapeType
    {
        get => _shapeType;
        set
        {
            if (_shapeType != value)
            {
                _shapeType = value;
                UpdateShapeBuilder(value);
                ShapeTypeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void UpdateShapeBuilder(ShapeType value)
    {
        switch (value)
        {
            case ShapeType.Rectangle:
                _currentBuilder = new RectangleShapeBuilder();
                break;
            case ShapeType.Oval:
                _currentBuilder = new OvalShapeBuilder();
                break;
            case ShapeType.Line:
                _currentBuilder = new LineShapeBuilder();
                break;
            //case ShapeType.Circle:
            //    break;
            case ShapeType.Triangle:
                _currentBuilder = new TriangleShapeBuilder();
                break;
            //case ShapeType.Star:
            //    break;
            //case ShapeType.Arrow:
            //    break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }
        //_currentBuilder.Initialize(DrawingService.DrawingLayer, DrawingService.CurrentColor, Opacity, LineThickness);
    }

    public ShapeTool(IDrawingService drawingService)
    {
        DrawingService = drawingService;
        DrawingService.DrawingTargetChanged += DrawingService_DrawingTargetChanged;
    }

    private void DrawingService_DrawingTargetChanged(object sender, EventArgs e)
    {
        if (!IsActive) return;
            
        _currentBuilder.SetNextPointPreview(_lastPoint);
        DrawingService.DrawingLayer.FinishCurrentDrawing();
    }

    public override Task Activate()
    {
        UpdateShapeBuilder(ShapeType);
        DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
        DrawingService.DrawingLayer.UseSwapBitmap = true;
            
        return base.Activate();
    }

    protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
    {
        _currentBuilder.Reset();
        _currentBuilder.BeginDrawing();
        _lastPoint = e.Pointer.GetPosition((SKNode) DrawingService.DrawingLayer);
        _currentBuilder.AddPoint(_lastPoint);
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        if (_currentBuilder.AddPointInputMode == AddPointInputMode.PressAndHold && !e.Pointer.IsPressed)
        {
            //_currentBuilder.Cancel();
            return;
        }

        _lastPoint = e.Pointer.GetPosition((SKNode) DrawingService.DrawingLayer);
        _currentBuilder.SetNextPointPreview(_lastPoint);
        DrawingService.DrawingLayer.FinishCurrentDrawing();
        DrawingService.Refresh();
    }

    protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
    {
        if (_currentBuilder.AddPointInputMode == AddPointInputMode.PressAndHold)
        {
            _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
        }
    }
}