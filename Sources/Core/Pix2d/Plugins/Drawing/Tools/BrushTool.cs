using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;
using SkiaNodes;
using SkiaNodes.Interactive;

namespace Pix2d.Plugins.Drawing.Tools;

public class BrushTool : BaseTool, IDrawingTool
{
    public IDrawingService DrawingService { get; }
    public ISelectionService SelectionService { get; }
    private SKNode _drawingLayerNode;
    private ShapeType _shapeType;
    private ShapeBuilderBase _currentBuilder;
    private BrushDrawingMode _drawingMode = BrushDrawingMode.Draw;

    private IDialogService DialogService => DefaultServiceLocator.ServiceLocatorProvider().GetInstance<IDialogService>();

    public virtual BrushDrawingMode DrawingMode
    {
        get => _drawingMode;
        protected set => _drawingMode = value;
    }

    public override EditContextType EditContextType => EditContextType.Sprite;

    public override string DisplayName => "Brush tool";
    public ShapeType ShapeType
    {
        get => _shapeType;
        set
        {
            if (_shapeType != value)
            {
                _shapeType = value;
                UpdateShapeBuilder();
            }
        }
    }

    public BrushTool(IDrawingService drawingService, ISelectionService selectionService)
    {
        DrawingService = drawingService;
        SelectionService = selectionService;
    }

    public override async Task Activate()
    {
        await base.Activate();
        try
        {
            _drawingLayerNode = (SKNode)DrawingService.DrawingLayer;

            DrawingService.DrawingTargetChanged += DrawingService_DrawingTargetChanged;

            _drawingLayerNode.PointerPressed += DrawingLayerNode_PointerPressed;

            UpdateShapeBuilder();
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

        DrawingService.DrawingTargetChanged -= DrawingService_DrawingTargetChanged;
        //DrawingService.SetDrawingMode(false);

        _drawingLayerNode.PointerPressed -= DrawingLayerNode_PointerPressed;

        DrawingService.DrawingLayer.ShowBrushPreview = false;
    }

    private void DrawingLayerNode_PointerPressed(object sender, PointerActionEventArgs e)
    {
        if (_shapeType == ShapeType.Free)
        {
            _drawingMode = !e.Pointer.IsEraser ? BrushDrawingMode.Draw : BrushDrawingMode.Erase;
            DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
        }
    }

    private void DrawingService_DrawingTargetChanged(object sender, EventArgs e)
    {
        _drawingLayerNode = (SKNode)DrawingService.DrawingLayer;
        UpdateDrawingMode();
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        base.OnPointerMoved(sender, e);

        DrawingService.DrawingLayer.ShowBrushPreview = !e.Pointer.IsTouch;

        //CHECK IF WE STILL ON OLD SPRITE
        if (!e.Pointer.IsPressed && !_drawingLayerNode.ContainsPoint(e.Pointer.WorldPosition))
        {
            var container = SelectionService.GetContainer(e.Pointer.WorldPosition);
            if (container is IDrawingTarget dt)
                DrawingService.SetDrawingTarget(dt);
        }

        if (e.Pointer.IsPressed && ShapeType != ShapeType.Free && _currentBuilder?.AddPointInputMode == AddPointInputMode.PressAndHold)
        {
            _currentBuilder?.SetNextPointPreview(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
        }
    }

    protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifier.Alt) != 0)
        {
            PickColorUnderPointer(e.Pointer);
            e.Handled = true;
        }

        if (!e.Handled && ShapeType != ShapeType.Free)
        {
            //drawing shapes
            _currentBuilder.Reset();
            _currentBuilder.BeginDrawing();
            _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
        }

        base.OnPointerPressed(sender, e);
    }

    protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
    {
        if (ShapeType != ShapeType.Free && _currentBuilder.AddPointInputMode == AddPointInputMode.PressAndHold)
        {
            _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
        }
    }

    private void PickColorUnderPointer(in SKInputPointer pointer)
    {
        DrawingService.PickColorByPoint(pointer.WorldPosition);
    }


    private void UpdateShapeBuilder()
    {
        switch (_shapeType)
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
            case ShapeType.Free: break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_shapeType), _shapeType, null);
        }

        _currentBuilder?.Initialize(DrawingService.DrawingLayer);

        UpdateDrawingMode();
    }

    private void UpdateDrawingMode()
    {
        if (_shapeType == ShapeType.Free)
        {
            DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
        }
        else
        {
            DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
        }
    }
}