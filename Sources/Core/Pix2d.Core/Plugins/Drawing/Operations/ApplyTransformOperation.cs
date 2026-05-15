using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Nodes;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

/// <summary>
/// Records the commit step of a pixel-selection transform — the moment <see cref="DrawingLayerNode"/> stamps the
/// lifted selection bitmap onto its <see cref="IDrawingTarget"/> and drops the marquee. Without this operation in
/// the stack the commit is invisible to redo: undoing the last <c>SelectionOperation</c> would rewind the canvas
/// to its pre-transform state, but redoing it would put us back into the lifted state, never to the actually
/// committed pixels.
/// </summary>
public class ApplyTransformOperation : EditOperationBase, ISpriteEditorOperation, IToolAwareOperation
{
    private readonly IDrawingTarget _drawingTarget;
    private readonly DrawingLayerNode _drawingLayer;
    private readonly SpriteSelectionNode _selectionLayer;
    private readonly SKBitmap _backgroundBitmap;
    private readonly byte[] _targetDataBefore;
    private readonly byte[] _targetDataAfter;

    // True when the commit was a "soft" hand-off to a selection tool that keeps the marquee alive in contour
    // mode (PixelTransformTool → Rect/Lasso/Color). False when the commit fully dropped the marquee
    // (PixelTransformTool → drawing/other tool). Determines whether redo re-creates the marquee or removes it.
    private readonly bool _keepMarqueeInContour;

    public HashSet<int> AffectedFrameIndexes { get; } = [];
    public HashSet<int> AffectedLayerIndexes { get; } = [];

    public string? ToolKeyBeforeOperation { get; }
    public string? ToolKeyAfterOperation { get; }

    public ApplyTransformOperation(
        IDrawingTarget drawingTarget,
        DrawingLayerNode drawingLayer,
        SpriteSelectionNode selectionLayer,
        SKBitmap backgroundBitmap,
        byte[] targetDataBefore,
        byte[] targetDataAfter,
        bool keepMarqueeInContour,
        string? toolKeyBefore,
        string? toolKeyAfter)
    {
        _drawingTarget = drawingTarget;
        _drawingLayer = drawingLayer;
        _selectionLayer = selectionLayer;
        _backgroundBitmap = backgroundBitmap;
        _targetDataBefore = targetDataBefore;
        _targetDataAfter = targetDataAfter;
        _keepMarqueeInContour = keepMarqueeInContour;
        ToolKeyBeforeOperation = toolKeyBefore;
        ToolKeyAfterOperation = toolKeyAfter;

        if (drawingTarget is IAnimatedNode animatedNode)
        {
            AffectedFrameIndexes.Add(animatedNode.CurrentFrameIndex);
            AffectedLayerIndexes.Add(animatedNode.SelectedLayerIndex);
        }
    }

    public override void OnPerform()
    {
        // Redo: the canvas state right before this commit lives in the previous SelectionOperation; what redo
        // adds is the stamp + marquee adjustment. Re-applying _targetDataAfter is enough because the lifted
        // bitmap has already been baked into it.
        _drawingTarget.SetData(_targetDataAfter);

        if (_keepMarqueeInContour)
        {
            // Hand-off to selection tool: marquee remains visible in contour mode at its post-commit position.
            // Use SetSelection rather than SetSelectionTransformMode so this works even when the editor is
            // currently inactive (e.g. another commit was redone first and dropped the marquee).
            _drawingLayer.SetSelection(_selectionLayer, _backgroundBitmap, contourOnly: true);
        }
        else if (_drawingLayer.HasSelection)
        {
            _drawingLayer.DeactivateSelectionEditor();
        }
    }

    public override void OnPerformUndo()
    {
        // Undo: rewind the canvas and put the marquee back in transform mode at the same position so the user
        // can continue tweaking from where the commit left them. Tool restoration will bring PixelTransformTool
        // back too — together that fully reconstructs the pre-commit state.
        _drawingTarget.SetData(_targetDataBefore);
        _drawingLayer.SetSelection(_selectionLayer, _backgroundBitmap, contourOnly: false);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingLayer;
    }
}
