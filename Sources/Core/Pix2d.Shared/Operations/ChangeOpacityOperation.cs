using SkiaNodes;

namespace Pix2d.Operations;

public class ChangeOpacityOperation(IEnumerable<SKNode> nodes) : ChangeNodePropertyOperationBase<float>(nodes)
{
    protected override float GetValue(SKNode node) => node.Opacity;

    protected override void SetValue(SKNode node, float value) => node.Opacity = value;
}