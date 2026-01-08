using Pix2d.Abstract.Drawing;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Extensions;

namespace Pix2d.Operations.Drawing;

public class PixelSelectOperation : EditOperationBase
{
    private readonly IDrawingLayer _drawingLayer;
    private SpriteNode _selectionLayer = null!;

    public PixelSelectOperation(IDrawingLayer drawingLayer)
    {
        _drawingLayer = drawingLayer;
        var selectionLayer = (_drawingLayer.GetSelectionLayer() as SpriteNode)?.Clone();
        _selectionLayer = selectionLayer ?? throw new InvalidOperationException("Failed to clone selection layer");
    }

    public override void OnPerform()
    {
        _drawingLayer.ActivateEditor();
        //_drawingLayer.SetData(_finalData);
    }

    public override void OnPerformUndo()
    {
        _drawingLayer.ApplySelection();
        //_drawingLayer.SetData(_initialData);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return (_drawingLayer as SKNode)!;
    }

}