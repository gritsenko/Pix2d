using System.Collections.Generic;
using Pix2d.Abstract.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations;

public class DuplicateLayerOperation : AddLayerOperation, ISpriteEditorOperation
{
    public HashSet<int> AffectedLayerIndexes { get; } = [];
    public HashSet<int> AffectedFrameIndexes { get; }

    public DuplicateLayerOperation(IEnumerable<SKNode> nodes, SKNode oldSelectedLayer) : base(nodes, oldSelectedLayer)
    {
    }
}