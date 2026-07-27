using System.Collections.Generic;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Nodes;
using Pix2d.Primitives.Operations;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

public class TransformSelectionOperation : EditOperationBase, IToolAwareOperation, ISelectionFlowOperation
{
    private readonly SelectionData _selectionData;
    private readonly SKNodeTransformState _initialState;
    private SKNodeTransformState? _finalState;
    private readonly DrawingLayerNode _drawingLayer;

    // Snapshot of the selection-phase at construction time so undo/redo can restore the editor in the same
    // mode the user was working in. Without this, an undo of a contour-mode reshape would dump the user into
    // transform mode (or vice versa), creating a mismatch with the restored tool.
    private readonly bool _wasContourOnly;

    public string? ToolKeyBeforeOperation { get; }
    public string? ToolKeyAfterOperation { get; private set; }

    public TransformSelectionOperation(DrawingLayerNode drawingLayer, string? activeToolKey = null)
    {
        _drawingLayer = drawingLayer;
        _selectionData = new SelectionData
        {
            SelectionLayer = (SpriteSelectionNode)drawingLayer.GetSelectionLayer(),
            BackgroundBitmap = drawingLayer.GetSelectionBackground(),
            DrawingTarget = drawingLayer.DrawingTarget ?? throw new InvalidOperationException("DrawingTarget cannot be null"),
            DrawingTargetData = drawingLayer.DrawingTarget?.GetData() ?? throw new InvalidOperationException("DrawingTargetData cannot be null"),
            SnapshotSize = drawingLayer.DrawingTarget!.GetSize(),
        };

        _initialState = new SKNodeTransformState(_selectionData.SelectionLayer);
        _wasContourOnly = drawingLayer.SelectionPhase == Primitives.Drawing.SelectionPhase.MarqueeReady;
        ToolKeyBeforeOperation = activeToolKey;
        ToolKeyAfterOperation = activeToolKey;
    }

    public TransformSelectionOperation(TransformSelectionOperation previousOperation)
    {
        _drawingLayer = previousOperation._drawingLayer;
        _selectionData = ResnapshotIfTargetResized(previousOperation);
        _initialState = new SKNodeTransformState(_selectionData.SelectionLayer);
        _wasContourOnly = previousOperation._wasContourOnly;
        ToolKeyBeforeOperation = previousOperation.ToolKeyAfterOperation;
        ToolKeyAfterOperation = previousOperation.ToolKeyAfterOperation;
    }

    /// <summary>
    /// Chained transform steps deliberately share one snapshot of the drawing target — the pixels as they
    /// were when the selection was lifted — so undoing any step in the chain rewinds to the same
    /// pre-selection canvas. That sharing is only valid while the snapshot still fits the target: a
    /// selection survives a canvas crop/resize, so cropping mid-selection leaves the shared buffer sized
    /// for the *old* canvas, and every step chained after the crop then pushed a wrong-sized buffer into
    /// <c>BitmapNode.SetData</c> — throwing <c>"Size of input data 1306260 is not equal to the size of
    /// the bitmap 295200"</c> (885x369x4 vs 200x369x4) on the first undo (appstat, 3.11.2). Take a fresh
    /// snapshot for the new step instead of inheriting the stale one; the earlier steps keep theirs,
    /// which is still the correct baseline for their own position in the history (below the crop).
    /// </summary>
    private static SelectionData ResnapshotIfTargetResized(TransformSelectionOperation previousOperation)
    {
        var previous = previousOperation._selectionData;
        var target = previous.DrawingTarget;
        var currentSize = target.GetSize();
        if (currentSize == previous.SnapshotSize)
            return previous;

        Logger.Trace($"Drawing target resized from {previous.SnapshotSize} to {currentSize} during a selection"
                     + " — re-snapshotting the selection-transform baseline.");

        return new SelectionData
        {
            SelectionLayer = previous.SelectionLayer,
            // The background is the same era as the data snapshot, so it is stale too. It is only used to
            // re-arm the marquee (never written back to the target), hence the fallback rather than a throw.
            BackgroundBitmap = TryGetSelectionBackground(previousOperation._drawingLayer) ?? previous.BackgroundBitmap,
            DrawingTarget = target,
            DrawingTargetData = target.GetData(),
            SnapshotSize = currentSize,
        };
    }

    private static SKBitmap? TryGetSelectionBackground(DrawingLayerNode drawingLayer)
    {
        try
        {
            return drawingLayer.GetSelectionBackground();
        }
        catch (Exception ex)
        {
            Logger.Trace($"Could not re-capture the selection background: {ex.Message}");
            return null;
        }
    }

    public void SetFinalState(string? activeToolKey = null)
    {
        _finalState = new SKNodeTransformState(_selectionData.SelectionLayer);
        if (activeToolKey != null)
            ToolKeyAfterOperation = activeToolKey;
    }

    public override void OnPerform()
    {
        _finalState?.ApplyTo(_selectionData.SelectionLayer);
        _selectionData.DrawingTarget.TryRestoreData(_selectionData.DrawingTargetData, nameof(TransformSelectionOperation));
        _drawingLayer.SetSelection(_selectionData.SelectionLayer, _selectionData.BackgroundBitmap, contourOnly: _wasContourOnly);
    }

    public override void OnPerformUndo()
    {
        _initialState.ApplyTo(_selectionData.SelectionLayer);
        _selectionData.DrawingTarget.TryRestoreData(_selectionData.DrawingTargetData, nameof(TransformSelectionOperation));
        _drawingLayer.SetSelection(_selectionData.SelectionLayer, _selectionData.BackgroundBitmap, contourOnly: _wasContourOnly);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingLayer;
    }

    private class SelectionData
    {
        public SpriteSelectionNode SelectionLayer { get; set; } = null!;
        public SKBitmap BackgroundBitmap { get; set; } = null!;
        public IDrawingTarget DrawingTarget { get; set; } = null!;
        public byte[] DrawingTargetData { get; set; } = null!;

        /// <summary>
        /// Size of <see cref="DrawingTarget"/> when <see cref="DrawingTargetData"/> was captured — the
        /// validity condition for reusing this snapshot in a chained operation (see
        /// <see cref="TransformSelectionOperation.ResnapshotIfTargetResized"/>).
        /// </summary>
        public SKSize SnapshotSize { get; set; }
    }
}