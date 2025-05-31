using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Operations.Drawing;
using Pix2d.Plugins.Ai.Selection;
using Pix2d.Primitives.Drawing;
using Pix2d.State;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Plugins.Ai;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    DisplayName = "Object selection tool",
    HotKey = null,
    Group = "Pixel Select",
    IconData = AiPlugin.ToolIcon
)]
public class ExtractObjectTool : BaseTool, IDrawingTool, IPixelSelectionTool
{
    private readonly IDrawingService _drawingService;
    private readonly IMessenger _messenger;
    private readonly AppState _appState;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private SelectionState SelectionState => _appState.SelectionState;

    private DrawingOperationWithFullState _pixelSelectDrawingOperation;
    private AiPixelSelector _aiPixelSelector;

    private IDrawingLayer DrawingLayer => _drawingService.DrawingLayer;

    public PixelSelectionMode SelectionMode
    {
        get => DrawingLayer.SelectionMode;
        set => DrawingLayer.SelectionMode = value;
    }

    public ExtractObjectTool(IDrawingService drawingService, IMessenger messenger, AppState appState, IViewPortRefreshService viewPortRefreshService)
    {
        _drawingService = drawingService;
        _messenger = messenger;
        _appState = appState;
        _viewPortRefreshService = viewPortRefreshService;
    }

    public override async Task Activate()
    {
        _pixelSelectDrawingOperation = null;

        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Select);
        DrawingLayer.PixelsBeforeSelected += DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.SelectionStarted += DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved += DrawingLayer_SelectionRemoved;
        DrawingLayer.PixelsSelected += DrawingLayer_PixelsSelected;

        _appState.UiState.ShowClipboardBar = true;

        await base.Activate();

        _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    private void DrawingLayer_PixelsSelected(object? sender, EventArgs e)
    {
        _viewPortRefreshService.Refresh();
    }

    private void DrawingLayer_SelectionRemoved(object? sender, EventArgs e)
    {
        SelectionState.IsUserSelecting = false;
    }

    private void DrawingLayer_SelectionStarted(object? sender, EventArgs e)
    {
        _aiPixelSelector = new AiPixelSelector();
        DrawingLayer.SetCustomPixelSelector(_aiPixelSelector);

        SelectionState.IsUserSelecting = true;
    }

    private void DrawingLayerOnPixelsBeforeSelected(object? sender, PixelsBeforeSelectedEventArgs e)
    {
        //ProcessSelectionBitmap(e.SelectionBitmap);

        //ViewPortService.Refresh();

        if (_pixelSelectDrawingOperation != null && DrawingLayer.HasSelectionChanges)
        {
            //                _pixelSelectDrawingOperation.SetFinalData();
            //                _pixelSelectDrawingOperation.PushToHistory();
        }

        _pixelSelectDrawingOperation = new DrawingOperationWithFullState(DrawingLayer.DrawingTarget);
    }

    private void ProcessSelectionBitmap(SKBitmap bitmap)
    {
        var bm = RemoveBackground.Process(bitmap, "u2netp.onnx");

        bm.CopyTo(bitmap);
    }

    protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
    {
        base.OnPointerMoved(sender, e);
        if (SelectionState.IsUserSelecting)
            SelectionState.UserSelectingFrameSize = DrawingLayer.SelectionSize;
    }

    private void OnOperationInvoked(OperationInvokedMessage e)
    {
        if (e.Operation.GetType().Name == "SelectionOperation")
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

        _appState.UiState.ShowClipboardBar = false;

        DrawingLayer.ClearCustomPixelSelector();
        DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.PixelsSelected -= DrawingLayer_PixelsSelected;
        DrawingLayer.SelectionStarted -= DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved -= DrawingLayer_SelectionRemoved;

        _messenger.Unregister<OperationInvokedMessage>(this, OnOperationInvoked);
        DrawingLayer.ApplySelection();
    }

}