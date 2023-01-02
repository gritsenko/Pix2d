using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class DeleteLayerOperation : DeleteNodesOperation
    {
        private Pix2dSprite _parent;
        private Pix2dSprite.Layer _deletedLayer;
        private int _oldIndex;
        private int _newIndex;

        public DeleteLayerOperation(IEnumerable<SKNode> nodes) : base(nodes)
        {
            _deletedLayer = nodes.FirstOrDefault() as Pix2dSprite.Layer;
            _oldIndex = _deletedLayer.Index;
            _newIndex = Math.Max(0,_deletedLayer.Index - 1);
            _parent = _deletedLayer.Parent as Pix2dSprite;
        }

        public override void OnPerform()
        {
            base.OnPerform();

            _parent.SelectLayer(_parent.Nodes[_newIndex] as Pix2dSprite.Layer);

        }

        public override void OnPerformUndo()
        {
            base.OnPerformUndo();
            _parent.SelectLayer(_deletedLayer);
        }
    }
}