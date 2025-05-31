using SkiaNodes;

namespace Pix2d.Primitives.Operations;

public class SKNodeState
{
    public SKNode TargetNode { get; set; }

    public SkNodeStructureState StructureState { get; set; }

    public SKNodeTransformState TransformState { get; set; }

    public SKNodeState(SKNode node)
    {
            TargetNode = node;
        }

    public void Apply()
    {
            StructureState?.ApplyTo(TargetNode);
            TransformState?.ApplyTo(TargetNode);
        }
}