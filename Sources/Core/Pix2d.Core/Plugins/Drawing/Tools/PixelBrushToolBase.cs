using System.Diagnostics;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using SkiaNodes;

namespace Pix2d.Plugins.Drawing.Tools;

public abstract class PixelBrushToolBase : BaseTool, IDrawingTool
{
    public IMessenger Messenger { get; }
    public IDrawingService DrawingService { get; }
    public ISelectionService SelectionService { get; }

    private SKNode? _drawingLayerNode;
    private BrushDrawingMode _drawingMode = BrushDrawingMode.Draw;

    public virtual BrushDrawingMode DrawingMode
    {
        get => _drawingMode;
        protected set => _drawingMode = value;
    }

    protected PixelBrushToolBase(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService)
    {
        Messenger = messenger;
        DrawingService = drawingService;
        SelectionService = selectionService;
    }

    public override async Task Activate()
    {
        await base.Activate();
        try
        {
            _drawingLayerNode = (SKNode)DrawingService.DrawingLayer;
            _drawingLayerNode.PointerPressed += DrawingLayerNode_PointerPressed;
            Messenger.Register<DrawingTargetChangedMessage>(this, DrawingServiceDrawingTargetChanged);

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

        if (_drawingLayerNode != null)
            _drawingLayerNode.PointerPressed -= DrawingLayerNode_PointerPressed;
        Messenger.Unregister<DrawingTargetChangedMessage>(this);

        DrawingService.DrawingLayer.ShowBrushPreview = false;
    }

    private void DrawingServiceDrawingTargetChanged(DrawingTargetChangedMessage msg)
    {
        _drawingLayerNode = (SKNode)DrawingService.DrawingLayer;
        DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
    }

    private void DrawingLayerNode_PointerPressed(object? sender, PointerActionEventArgs e)
    {
        _drawingMode = !e.Pointer.IsEraser ? BrushDrawingMode.Draw : BrushDrawingMode.Erase;
        DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
    }

    protected override void OnPointerMoved(object? sender, PointerActionEventArgs e)
    {
        DrawingService.DrawingLayer.ShowBrushPreview = !e.Pointer.IsTouch;
    }

    protected override void OnPointerPressed(object? sender, PointerActionEventArgs e)
    {
        // Click-to-activate artboard: a press outside the current drawing layer (i.e. on another sprite)
        // makes that sprite the active edit target. The drawing layer lives on the active sprite, so this
        // press never starts a stroke on the wrong artboard — the first click focuses, later strokes draw.
        if (_drawingLayerNode != null
            && !_drawingLayerNode.ContainsPoint(e.Pointer.WorldPosition)
            && SelectionService.GetContainer(e.Pointer.WorldPosition) is Pix2dSprite sprite)
        {
            Messenger.Send(new ActivateArtboardRequestedMessage(sprite));
        }

        if ((e.KeyModifiers & KeyModifier.Alt) == 0) return;

        DrawingService.PickColorByPoint(e.Pointer.WorldPosition);
        e.Handled = true;
    }
}