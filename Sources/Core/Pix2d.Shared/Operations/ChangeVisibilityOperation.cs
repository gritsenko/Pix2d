using SkiaNodes;

namespace Pix2d.Operations;

public class ChangeVisibilityOperationBase(IEnumerable<SKNode> nodes) : ChangeNodePropertyOperationBase<bool>(nodes)
{
    protected override bool GetValue(SKNode node) => node.IsVisible;

    protected override void SetValue(SKNode node, bool value) => node.IsVisible = value;
}