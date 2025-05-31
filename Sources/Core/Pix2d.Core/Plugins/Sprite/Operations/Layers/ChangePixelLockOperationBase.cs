using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations.Layers;

public class ChangePixelLockOperationBase(IEnumerable<SKNode> nodes) : ChangeNodePropertyOperationBase<bool>(nodes), ISpriteEditorOperation
{
    protected override bool GetValue(SKNode node) => node.IsVisible;

    protected override void SetValue(SKNode node, bool value) => node.IsVisible = value;
    public HashSet<int> AffectedFrameIndexes { get; private set; }
    public HashSet<int> AffectedLayerIndexes { get; private set; }

    public override void SetFinalData()
    {
        base.SetFinalData();

        var firstNode = nodes.FirstOrDefault();

        AffectedFrameIndexes = [];
        AffectedLayerIndexes = nodes.Select(x => x.Index).ToHashSet();
    }
}