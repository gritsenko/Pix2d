using SkiaNodes;

namespace Pix2d.Primitives.Operations
{
    public class SkNodeStructureState
    {
        public int NodeIndex { get; set; }
        public SKNode Parent { get; set; }

        public SkNodeStructureState(SKNode node)
        {
            NodeIndex = node.Index;
            Parent = node.Parent;
        }

        public void ApplyTo(SKNode node)
        {
            Parent.Nodes.Insert(NodeIndex, node);
        }
    }
}