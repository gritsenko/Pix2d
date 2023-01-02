using System.Collections.Generic;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class DuplicateLayerOperation : AddLayerOperation
    {
        public DuplicateLayerOperation(IEnumerable<SKNode> nodes, SKNode oldSelectedLayer) : base(nodes, oldSelectedLayer)
        {
        }
    }
}