using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Operations.Effects;

public class RemoveEffectOperation(SKNode node, ISKNodeEffect effect) : EditOperationBase
{
    public override void OnPerform()
    {
        node.Effects.Remove(effect);
    }

    public override void OnPerformUndo()
    {
        node.Effects.Add(effect);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return node.Yield();
    }
}