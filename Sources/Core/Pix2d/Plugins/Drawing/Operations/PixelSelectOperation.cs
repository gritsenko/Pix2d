using System.Collections.Generic;
using Pix2d.CommonNodes;
using Pix2d.Drawing.Nodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Extensions;

namespace Pix2d.Plugins.Drawing.Operations
{
    public class PixelSelectOperation : EditOperationBase
    {
        private readonly DrawingLayerNode _drawingLayer;
        private SpriteNode _selectionLayer;

        public PixelSelectOperation(DrawingLayerNode drawingLayer)
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
}