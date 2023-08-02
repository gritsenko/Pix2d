using System.Collections.Generic;
using Pix2d.Abstract.Drawing;
using Pix2d.Drawing.Nodes;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Nodes;
using Pix2d.Primitives.Operations;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

public class SelectionOperation : EditOperationBase
{
    private readonly SelectionData _selectionData;
    private readonly SKNodeTransformState _initialState;
    private SKNodeTransformState _finalState;
    private readonly DrawingLayerNode _drawingLayer;

    public SelectionOperation(DrawingLayerNode drawingLayer)
    {
        _drawingLayer = drawingLayer;
        _selectionData = new SelectionData
        {
            SelectionLayer = (SpriteSelectionNode)drawingLayer.GetSelectionLayer(),
            SelectionBackground = drawingLayer.GetSelectionBackground(),
            DrawingTarget = drawingLayer.DrawingTarget,
            DrawingTargetData = drawingLayer.DrawingTarget.GetData(),
        };

        _initialState = new SKNodeTransformState(_selectionData.SelectionLayer);
    }

    public SelectionOperation(SelectionOperation previousOperation)
    {
        _drawingLayer = previousOperation._drawingLayer;
        _selectionData = previousOperation._selectionData;
        _initialState = new SKNodeTransformState(_selectionData.SelectionLayer);
    }

    public void SetFinalState()
    {
        _finalState = new SKNodeTransformState(_selectionData.SelectionLayer);
    }
    
    public override void OnPerform()
    {
        _finalState.ApplyTo(_selectionData.SelectionLayer);
        _selectionData.DrawingTarget.SetData(_selectionData.DrawingTargetData);
        _drawingLayer.SetSelection(_selectionData.SelectionLayer, _selectionData.SelectionBackground);
    }

    public override void OnPerformUndo()
    {
        _initialState.ApplyTo(_selectionData.SelectionLayer);
        _selectionData.DrawingTarget.SetData(_selectionData.DrawingTargetData);
        _drawingLayer.SetSelection(_selectionData.SelectionLayer, _selectionData.SelectionBackground);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingLayer;
    }

    private class SelectionData
    {
        public SpriteSelectionNode SelectionLayer { get; set; }
        public SKBitmap SelectionBackground { get; set; }
        public IDrawingTarget DrawingTarget { get; set; }
        public byte[] DrawingTargetData { get; set; }
    }
}