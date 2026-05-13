using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Tools.PixelSelect;

/// <summary>
/// Base for the selection tools (Rect / Lasso / Color). Their sole job is to describe the marquee area — they
/// never lift pixels or modify the drawing target. Lifting / transforming / committing is owned by
/// <see cref="PixelTransformTool"/>. This split keeps undo/redo coherent across tool switches: a selection
/// op produced here always restores to a selection tool, never to the transform tool.
/// </summary>
public abstract class PixelSelectToolBase : BaseTool, IDrawingTool, IPixelSelectionTool
{
    public IDrawingService DrawingService { get; }
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    public SelectionState SelectionState => AppState.SelectionState;
    protected IToolService ToolService { get; }

    private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

    public PixelSelectionMode SelectionMode
    {
        get => DrawingLayer.SelectionMode;
        set => DrawingLayer.SelectionMode = value;
    }

    public PixelSelectToolBase(IDrawingService drawingService, IMessenger messenger, AppState state, IToolService toolService)
    {
        DrawingService = drawingService;
        Messenger = messenger;
        AppState = (AppState)state;
        ToolService = toolService;
    }

    public override async Task Activate()
    {
        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Select);
        DrawingLayer.PixelsBeforeSelected += DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.SelectionStarted += DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved += DrawingLayer_SelectionRemoved;
        AppState.UiState.ShowClipboardBar = true;

        // Restoration path: when undo/redo lands us on a selection tool while the layer is in transform mode
        // (e.g. selection was created via paste, then undone), step the editor back to contour-only so the
        // tool state matches what the user expects from a selection tool. Safe no-op when the marquee is
        // already in contour mode or absent.
        if (DrawingLayer.SelectionPhase == SelectionPhase.Transforming)
            DrawingLayer.SetSelectionTransformMode(false);

        await base.Activate();
    }

    private void DrawingLayer_SelectionRemoved(object? sender, EventArgs e)
    {
        SelectionState.IsUserSelecting = false;
    }

    private void DrawingLayer_SelectionStarted(object? sender, EventArgs e)
    {
        SelectionState.IsUserSelecting = true;
    }

    private void DrawingLayerOnPixelsBeforeSelected(object? sender, EventArgs e)
    {
    }

    protected override void OnPointerMoved(object? sender, PointerActionEventArgs e)
    {
        base.OnPointerMoved(sender, e);
        if (SelectionState.IsUserSelecting)
        {
            if (!SelectionState.UserSelectingFrameSize.Equals(DrawingLayer.SelectionSize))
                SelectionState.UserSelectingFrameSize = DrawingLayer.SelectionSize;
        }
    }

    public override void Deactivate()
    {
        base.Deactivate();

        AppState.UiState.ShowClipboardBar = false;

        DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
        DrawingLayer.SelectionStarted -= DrawingLayer_SelectionStarted;
        DrawingLayer.SelectionRemoved -= DrawingLayer_SelectionRemoved;

        // Hand off cleanly to PixelTransformTool: that tool's whole job is to take ownership of the current
        // marquee, so dropping it here would leave it nothing to work with (and it would just bounce us back
        // to this tool, creating a loop). For any other transition we drop the marquee — drawing tools don't
        // honour selections and leaving the editor alive would let its thumbs intercept brush strokes.
        if (ToolService.IncomingToolKey != nameof(PixelTransformTool))
            DrawingLayer.ApplySelection();

        if (DrawingLayer.DrawingTarget != null)
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