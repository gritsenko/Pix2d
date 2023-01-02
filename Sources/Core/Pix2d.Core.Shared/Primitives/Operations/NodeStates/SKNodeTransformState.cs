using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Primitives.Operations
{
    public class SKNodeTransformState
    {
        public SKPoint Position { get; set; }

        public SKSize Size { get; set; }
        public float Rotation { get; set; }

        public SKNodeTransformState(SKNode node)
        {
            Position = new SKPoint(node.Position.X, node.Position.Y);
            Rotation = node.Rotation;
            Size = node.Size;
        }

        public void ApplyTo(SKNode node)
        {
            node.Position = Position;
            node.Rotation = Rotation;
            node.Size = Size;
        }
    }
}
