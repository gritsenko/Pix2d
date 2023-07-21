using Mvvm.Messaging;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Primitives.Drawing;
using Pix2d.State;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Plugins.Ai.Selection;

public class ExtractObjectTool : BaseTool, IDrawingTool, IPixelSelectionTool
{
    public static ToolSettings ToolSettings { get; } = new()
    {
        DisplayName = "Object selection tool",
        HotKey = null,
        IconData = AiPlugin.ToolIcon,
    };

    public override string DisplayName => ToolSettings.DisplayName;

    public override string ToolIconData => AiPlugin.ToolIcon;

    public IDrawingService DrawingService { get; }
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    public IViewPortService ViewPortService { get; }
    public SelectionState SelectionState => AppState.SelectionState;

    private DrawingOperation _pixelSelectDrawingOperation;
    private AiPixelSelector _aiPixelSelector;

    private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

    public override EditContextType EditContextType => EditContextType.Sprite;

    public PixelSelectionMode SelectionMode
    {
        get => DrawingLayer.SelectionMode;
        set => DrawingLayer.SelectionMode = value;
    }

    public ExtractObjectTool(IDrawingService drawingService, IMessenger messenger, AppState appState, IViewPortService viewPortService)
    {
        DrawingService = drawingService;
        Messenger = messenger;
        AppState = appState;
        ViewPortService = viewPortService;
    }

    public override async Task Activate()
    {
        _pixelSelectDrawingOperation = null;

        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Select);
        DrawingLayer.PixelsBeforeSelected += DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.SelectionStarted += DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved += DrawingLayer_SelectionRemoved;
        DrawingLayer.PixelsSelected += DrawingLayer_PixelsSelected;

        AppState.UiState.ShowClipboardBar = true;

        await base.Activate();

        Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    private void DrawingLayer_PixelsSelected(object? sender, EventArgs e)
    {
        ViewPortService.Refresh();
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

        _pixelSelectDrawingOperation = new DrawingOperation(DrawingLayer.DrawingTarget);
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

        DrawingLayer.ClearCustomPixelSelector();
        DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.PixelsSelected -= DrawingLayer_PixelsSelected;
        DrawingLayer.SelectionStarted -= DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved -= DrawingLayer_SelectionRemoved;

        Messenger.Unregister<OperationInvokedMessage>(this, OnOperationInvoked);
        DrawingLayer.ApplySelection();
    }

}