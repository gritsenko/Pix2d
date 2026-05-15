using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.UI;
using Pix2d.Primitives.Drawing;

namespace Pix2d.Plugins.Drawing.Tools.PixelSelect;

/// <summary>
/// Dedicated tool for transforming a pixel selection (move / resize / rotate / flip). It is the only tool that
/// "lifts" the selected pixels from the canvas onto the selection layer and that commits them back. Selection
/// tools (Rect / Lasso / Color) only describe the marquee area in contour-only mode and never touch the
/// drawing target directly.
/// </summary>
[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = true,
    SettingsViewType = typeof(SelectionTransformToolSettingsView),
    DisplayName = "Transform selection",
    Group = "Pixel Select",
    // Real binding lives on SpriteEditCommands.ActivateSelectionTransform (Ctrl+Shift+T). Plain "T"
    // is already taken by ToolCommands.ActivateTriangleTool, so we must not advertise it here.
    HotKey = "Ctrl+Shift+T")]
public class PixelTransformTool : BaseTool, IDrawingTool, IPixelSelectionTool
{
    private readonly IDrawingService _drawingService;
    private readonly IToolService _toolService;
    private readonly AppState _appState;

    private IDrawingLayer DrawingLayer => _drawingService.DrawingLayer;
    private SelectionState SelectionState => _appState.SelectionState;

    public PixelSelectionMode SelectionMode
    {
        get => DrawingLayer.SelectionMode;
        set => DrawingLayer.SelectionMode = value;
    }

    public PixelTransformTool(IDrawingService drawingService, IToolService toolService, AppState appState)
    {
        _drawingService = drawingService;
        _toolService = toolService;
        _appState = appState;
    }

    public override Task Activate()
    {
        // Transform tool is only useful when there is an existing selection. Without one we fall back to the
        // rectangular selection tool so the user can describe an area first; this matches the user's intent
        // when they press the Transform hotkey without a selection in place.
        if (!DrawingLayer.HasSelection)
        {
            _toolService.ActivateTool<PixelSelectRectTool>();
            return Task.CompletedTask;
        }

        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.MoveSelection);
        // MoveSelection mode gates pointer events the way we want (clicks fall through to FrameEditorNode's
        // thumbs) but its side-effect is AllowResize=false — meant for text/AI tools that want move-only
        // semantics. Override it back to true so the transform tool actually shows its resize thumbs.
        DrawingLayer.AllowSelectionResize = true;
        // Enter transform mode. Calling ActivateEditor(contourOnly: false) directly preserves the original
        // selection path (lasso/freeform contours stay intact) — using SetSelectionTransformMode(true) would
        // route through LiftSelectionFromCanvas which rewrites the selection to an axis-aligned rect.
        if (DrawingLayer.SelectionPhase == SelectionPhase.MarqueeReady)
            DrawingLayer.ActivateEditor(contourOnly: false);

        _appState.UiState.ShowClipboardBar = true;

        return base.Activate();
    }

    public override void Deactivate()
    {
        base.Deactivate();

        _appState.UiState.ShowClipboardBar = false;

        // Commit only if we're still actually in transform mode. If undo/redo already stepped the layer
        // back to contour (e.g. an undo passed through a contour-mode op while this tool was active), the
        // marquee is in MarqueeReady now and committing would also call DeactivateSelectionEditor — which
        // would destroy the marquee the user (or undo) wants to keep.
        if (DrawingLayer.SelectionPhase == SelectionPhase.Transforming)
        {
            // Hand-off to a selection tool keeps the marquee alive in contour mode so the receiving tool
            // inherits it; anything else (drawing tools, paste, closing the sprite, …) drops it. Either way
            // the stamp is recorded as a single undo step via CommitTransformWithUndo so redo can replay it.
            _drawingService.CommitTransformWithUndo(
                keepMarqueeInContour: IsHandoffToSelectionTool(),
                toolKeyBefore: nameof(PixelTransformTool),
                toolKeyAfter: _toolService.IncomingToolKey);
        }

        SelectionState.IsUserSelecting = false;
    }

    private bool IsHandoffToSelectionTool() =>
        _toolService.IncomingToolKey is nameof(PixelSelectRectTool)
            or nameof(PixelSelectLassoTool)
            or nameof(PixelSelectColorTool);
}
