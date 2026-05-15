using System.Collections.Generic;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Nodes;
using Pix2d.Primitives.Operations;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

public class TransformSelectionOperation : EditOperationBase, IToolAwareOperation
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
        };

        _initialState = new SKNodeTransformState(_selectionData.SelectionLayer);
        _wasContourOnly = drawingLayer.SelectionPhase == Primitives.Drawing.SelectionPhase.MarqueeReady;
        ToolKeyBeforeOperation = activeToolKey;
        ToolKeyAfterOperation = activeToolKey;
    }

    public TransformSelectionOperation(TransformSelectionOperation previousOperation)
    {
        _drawingLayer = previousOperation._drawingLayer;
        _selectionData = previousOperation._selectionData;
        _initialState = new SKNodeTransformState(_selectionData.SelectionLayer);
        _wasContourOnly = previousOperation._wasContourOnly;
        ToolKeyBeforeOperation = previousOperation.ToolKeyAfterOperation;
        ToolKeyAfterOperation = previousOperation.ToolKeyAfterOperation;
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
        _selectionData.DrawingTarget.SetData(_selectionData.DrawingTargetData);
        _drawingLayer.SetSelection(_selectionData.SelectionLayer, _selectionData.BackgroundBitmap, contourOnly: _wasContourOnly);
    }

    public override void OnPerformUndo()
    {
        _initialState.ApplyTo(_selectionData.SelectionLayer);
        _selectionData.DrawingTarget.SetData(_selectionData.DrawingTargetData);
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
    }
}