using System;
using System.Threading.Tasks;
using CommonServiceLocator;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.State;
using Pix2d.Abstract.Tools;
using Pix2d.Abstract.UI;
using Pix2d.Drawing.Nodes;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Primitives.Drawing;
using Pix2d.State;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Drawing.Tools;

public class PixelSelectTool : BaseTool, IDrawingTool, IPixelSelectionTool
{
    public IDrawingService DrawingService { get; }
    public IMessenger Messenger { get; }
    public AppState State { get; }
    public ISelectionState SelectionState => State.SelectionState;

    private DrawingOperation _pixelSelectDrawingOperation;
    public override string DisplayName => "Pixels select tool";

    private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

    public override EditContextType EditContextType => EditContextType.Sprite;

    public PixelSelectionMode SelectionMode
    {
        get => DrawingLayer.SelectionMode;
        set => DrawingLayer.SelectionMode = value;
    }

    public PixelSelectTool(IDrawingService drawingService, IMessenger messenger, IAppState state)
    {
        DrawingService = drawingService;
        Messenger = messenger;
        State = (AppState)state;
    }

    public override async Task Activate()
    {
        _pixelSelectDrawingOperation = null;

        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Select);
        DrawingLayer.PixelsBeforeSelected += DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.SelectionStarted += DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved += DrawingLayer_SelectionRemoved;
        var mc = ServiceLocator.Current.GetInstance<IMenuController>();
        mc.ShowClipboardBar = true;

        await base.Activate();

        Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    private void DrawingLayer_SelectionRemoved(object sender, EventArgs e)
    {
        SelectionState.Set(x => x.IsUserSelecting, false);
    }

    private void DrawingLayer_SelectionStarted(object sender, EventArgs e)
    {
        SelectionState.Set(x => x.IsUserSelecting, true);
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
            SelectionState.Set(x => x.UserSelectingFrameSize, DrawingLayer.SelectionSize);
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

        var mc = ServiceLocator.Current.GetInstance<IMenuController>();
        mc.ShowClipboardBar = false;

        DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
        Messenger.Unregister<OperationInvokedMessage>(this, OnOperationInvoked);
        DrawingLayer.ApplySelection();
    }

    public SKRect GetSelectionRect()
    {
        var selectionLayer = DrawingLayer.GetSelectionLayer();
        return selectionLayer.GetBoundingBox();
    }

    public void ApplySelection()
    {
        DrawingLayer.ApplySelection();
    }
}