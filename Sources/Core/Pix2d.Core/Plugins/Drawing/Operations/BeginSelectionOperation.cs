using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Nodes;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

/// <summary>
/// Records the moment a user finishes drawing a fresh marquee (Rect/Lasso/Color drag or Select-All).
/// Pushed exactly once per user-initiated marquee from <c>DrawingService</c> in response to
/// <see cref="DrawingLayerNode.MarqueeFinishedByUser"/>. Without this op the marquee creation is invisible
/// to undo, so a user transforming a selection couldn't fully reverse out of the "I have a marquee" state.
/// </summary>
public class BeginSelectionOperation : EditOperationBase, IToolAwareOperation, ISelectionFlowOperation
{
    private readonly DrawingLayerNode _drawingLayer;
    private readonly SpriteSelectionNode _selectionLayer;
    private readonly SKBitmap _backgroundBitmap;
    private readonly SelectionStateSnapshot? _previousSelection;

    public string? ToolKeyBeforeOperation { get; }
    public string? ToolKeyAfterOperation { get; }

    /// <param name="previousSelection">
    /// The selection this marquee was combined with (Shift/Ctrl), if any. Undo restores it instead of
    /// clearing — a Shift-add is one step *on top of* a selection, so reversing it has to leave that
    /// selection standing, otherwise building one up region by region would collapse on the first Ctrl+Z.
    /// Null for an ordinary replacing marquee, which undoes to nothing selected.
    /// </param>
    public BeginSelectionOperation(
        DrawingLayerNode drawingLayer,
        SpriteSelectionNode selectionLayer,
        SKBitmap backgroundBitmap,
        string? toolKey,
        SelectionStateSnapshot? previousSelection = null)
    {
        _drawingLayer = drawingLayer;
        _selectionLayer = selectionLayer;
        _backgroundBitmap = backgroundBitmap;
        _previousSelection = previousSelection;

        // Both before and after point at the selection tool that produced the marquee. On undo this avoids
        // stranding the user in PixelTransformTool with no selection (which is its own broken state) — the
        // tool restoration on the prior ops walks us back through PixelTransformTool, and this last step
        // pops out into a selection tool ready to draw a new marquee. Redo replays the original tool.
        ToolKeyBeforeOperation = toolKey;
        ToolKeyAfterOperation = toolKey;
    }

    public override void OnPerform()
    {
        // Redo: replay the marquee creation in contour mode. Pixels stay on the canvas — selection tools
        // never lift, and any subsequent TransformSelectionOperation / ApplyTransformOperation on the redo path
        // will replay the lift / transform / commit cycle.
        _drawingLayer.SetSelection(_selectionLayer, _backgroundBitmap, contourOnly: true);
    }

    public override void OnPerformUndo()
    {
        if (_previousSelection != null)
        {
            _drawingLayer.SetSelection(
                _previousSelection.SelectionLayer,
                _previousSelection.BackgroundBitmap,
                contourOnly: _previousSelection.ContourOnly);
            return;
        }

        // Undo: drop the marquee. If a stacked operation already dropped it (rare but possible when the
        // user creates multiple marquees in a row), this is a no-op rather than an error.
        if (_drawingLayer.HasSelection)
            _drawingLayer.DeactivateSelectionEditor();
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingLayer;
    }
}
