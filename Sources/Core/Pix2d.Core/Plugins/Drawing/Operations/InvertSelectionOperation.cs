using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Common.Drawing;
using Pix2d.Plugins.Drawing.Nodes;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

internal class InvertSelectionOperation : EditOperationBase, IToolAwareOperation, ISelectionFlowOperation
{
    private readonly DrawingLayerNode _drawingLayer;
    private readonly SelectionStateSnapshot _beforeState;
    private readonly SelectionStateSnapshot? _afterState;

    public string? ToolKeyBeforeOperation { get; }
    public string? ToolKeyAfterOperation { get; }

    public InvertSelectionOperation(
        DrawingLayerNode drawingLayer,
        SelectionStateSnapshot beforeState,
        SelectionStateSnapshot? afterState,
        string? toolKeyBefore,
        string? toolKeyAfter)
    {
        _drawingLayer = drawingLayer;
        _beforeState = beforeState;
        _afterState = afterState;
        ToolKeyBeforeOperation = toolKeyBefore;
        ToolKeyAfterOperation = toolKeyAfter;
    }

    public override void OnPerform()
    {
        ApplyState(_afterState);
    }

    public override void OnPerformUndo()
    {
        ApplyState(_beforeState);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingLayer;
    }

    private void ApplyState(SelectionStateSnapshot? state)
    {
        if (state == null)
        {
            if (_drawingLayer.HasSelection)
                _drawingLayer.DeactivateSelectionEditor();

            return;
        }

        _drawingLayer.SetSelection(state.SelectionLayer, state.BackgroundBitmap, contourOnly: state.ContourOnly);
    }

    /// <summary>
    /// Inverting is the same round trip the Shift/Ctrl marquee combining uses — flatten the live selection
    /// to a canvas mask, do the set algebra, rebuild — with the algebra being a straight negation.
    /// </summary>
    internal static SelectionStateSnapshot? CreateInvertedSelectionState(
        IDrawingTarget drawingTarget,
        SpriteSelectionNode currentSelection,
        SKBitmap sourceBitmap)
    {
        var targetPosition = ((SKNode)drawingTarget).Position;

        var mask = SelectionMaskOps.Rasterize(currentSelection, targetPosition, sourceBitmap.Width, sourceBitmap.Height);
        SelectionMaskOps.InvertInPlace(mask);

        var region = SelectionMaskOps.BuildRegion(sourceBitmap, mask, targetPosition);
        return region == null
            ? null
            : new SelectionStateSnapshot(region.SelectionLayer, region.BackgroundBitmap, ContourOnly: true);
    }
}
