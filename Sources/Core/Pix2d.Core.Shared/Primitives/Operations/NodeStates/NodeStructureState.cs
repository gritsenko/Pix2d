using SkiaNodes;

namespace Pix2d.Abstract.Operations
{
    public class NodeStructureState
    {
        public readonly SKNode Node;
        public readonly SKNode Parent;
        public readonly int Index;
        public readonly int NestingLevel;

        public NodeStructureState(SKNode node)
        {
            Node = node;
            Parent = node.Parent;
            Index = node.Index;
            NestingLevel = node.GetNestingLevel();
        }

        public virtual void RestoreNodeState()
        {
            Parent.Nodes.Insert(Index, Node);
        }
    }
}