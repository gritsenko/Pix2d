using Pix2d.Abstract.Drawing;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Extensions;

namespace Pix2d.Operations.Drawing;

public class PixelSelectOperation : EditOperationBase
{
    private readonly IDrawingLayer _drawingLayer;
    private SpriteNode _selectionLayer;

    public PixelSelectOperation(IDrawingLayer drawingLayer)
    {
        _drawingLayer = drawingLayer;
        _selectionLayer = (_drawingLayer.GetSelectionLayer() as SpriteNode).Clone();
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
        yield return _drawingLayer as SKNode;
    }

}