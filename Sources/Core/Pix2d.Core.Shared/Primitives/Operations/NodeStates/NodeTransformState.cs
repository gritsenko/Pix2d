using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Abstract.Operations
{
    public class NodeTransformState : NodeStructureState
    {
        public SKPoint Position { get; set; }
        public SKSize Size { get; set; }
        public float Rotation { get; set; }

        public NodeTransformState(SKNode node) : base(node)
        {
            Position = node.Position;
            Rotation = node.Rotation;
            Size = node.Size;
        }

        public override void RestoreNodeState()
        {
            base.RestoreNodeState();

            Node.Position = Position;
            Node.Rotation = Rotation;
            Node.Size = Size;
        }

    }
}