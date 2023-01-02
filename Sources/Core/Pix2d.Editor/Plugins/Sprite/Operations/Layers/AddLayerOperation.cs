using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class AddLayerOperation : CreateNodesOperation
    {
        private readonly Pix2dSprite.Layer _oldSelectedLayer;
        private Pix2dSprite _parent;
        private Pix2dSprite.Layer _newLayer;

        public AddLayerOperation(IEnumerable<SKNode> nodes, SKNode oldSelectedLayer) : base(nodes)
        {
            _oldSelectedLayer = oldSelectedLayer as Pix2dSprite.Layer;
            _parent = _oldSelectedLayer.Parent as Pix2dSprite;
            _newLayer = nodes.FirstOrDefault() as Pix2dSprite.Layer;
        }

        public override void OnPerform()
        {
            base.OnPerform();

            _parent.SelectLayer(_newLayer);

        }

        public override void OnPerformUndo()
        {
            base.OnPerformUndo();
            _parent.SelectLayer(_oldSelectedLayer, true);
        }
    }
}