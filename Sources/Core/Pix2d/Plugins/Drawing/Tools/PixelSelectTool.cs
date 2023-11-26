using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Nodes;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Primitives.Drawing;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Drawing.Tools;


[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = true, 
    DisplayName = "Pixels select tool")]
public class PixelSelectTool : BaseTool, IDrawingTool, IPixelSelectionTool
{
    public IDrawingService DrawingService { get; }
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    public SelectionState SelectionState => AppState.SelectionState;

    private DrawingOperation _pixelSelectDrawingOperation;

    private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

    public PixelSelectionMode SelectionMode
    {
        get => DrawingLayer.SelectionMode;
        set => DrawingLayer.SelectionMode = value;
    }

    public PixelSelectTool(IDrawingService drawingService, IMessenger messenger, AppState state)
    {
        DrawingService = drawingService;
        Messenger = messenger;
        AppState = (AppState)state;
    }

    public override async Task Activate()
    {
        _pixelSelectDrawingOperation = null;

        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Select);
        DrawingLayer.PixelsBeforeSelected += DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.SelectionStarted += DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved += DrawingLayer_SelectionRemoved;
        AppState.UiState.ShowClipboardBar = true;

        await base.Activate();

        Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    private void DrawingLayer_SelectionRemoved(object sender, EventArgs e)
    {
        SelectionState.IsUserSelecting = false;
    }

    private void DrawingLayer_SelectionStarted(object sender, EventArgs e)
    {
        SelectionState.IsUserSelecting = true;
    }

    private void DrawingLayerOnPixelsBeforeSelected(object sender, EventArgs e)
    {
        if (_pixelSelectDrawingOperation != null && DrawingLayer.HasSelectionChanges)
        {
//                _pixelSelectDrawingOperation.SetFinalData();
//                _pixelSelectDrawingOperation.PushToHistory();
        }

        _pixelSelectDrawingOperation = new DrawingOperation(((DrawingLayerNode)DrawingLayer).DrawingTarget);
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        base.OnPointerMoved(sender, e);
        if (SelectionState.IsUserSelecting)
            SelectionState.UserSelectingFrameSize = DrawingLayer.SelectionSize;
    }

    private void OnOperationInvoked(OperationInvokedMessage e)
    {
        if (e.Operation is SelectionOperation || e.Operation is PasteOperation)
        {
            return;
        }
        
        if (e.Operation is MoveOperation)
        {
            if (e.OperationType != OperationEventType.Perform)
                DrawingLayer.InvalidateSelectionEditor();
        }
        else
        {
            DrawingLayer.DeactivateSelectionEditor();
        }
    }

    public override void Deactivate()
    {
        base.Deactivate();

        AppState.UiState.ShowClipboardBar = false;

        DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
        Messenger.Unregister<OperationInvokedMessage>(this, OnOperationInvoked);
        DrawingLayer.ApplySelection();
        SelectionState.UserSelectingFrameSize = DrawingLayer.DrawingTarget.GetSize();
    }

    public SKRect GetSelectionRect()
    {
        var selectionLayer = DrawingLayer.GetSelectionLayer();
        return selectionLayer?.GetBoundingBox() ?? default;
    }

    public void ApplySelection()
    {
        DrawingLayer.ApplySelection();
    }
}