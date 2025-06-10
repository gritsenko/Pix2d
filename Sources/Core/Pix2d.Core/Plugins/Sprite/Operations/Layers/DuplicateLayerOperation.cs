using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations;

public class DuplicateLayerOperation(IEnumerable<SKNode> nodes, SKNode oldSelectedLayer)
    : AddLayerOperation(nodes, oldSelectedLayer)
{
}