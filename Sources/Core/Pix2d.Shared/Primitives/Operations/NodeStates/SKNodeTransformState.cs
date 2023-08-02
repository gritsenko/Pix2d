using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Primitives.Operations
{
    public class SKNodeTransformState
    {
        public SKPoint Position { get; set; }

        public SKSize Size { get; set; }
        public float Rotation { get; set; }
        public SKPoint PivotPosition { get; set; }

        public SKNodeTransformState(SKNode node)
        {
            Position = node.Position;
            PivotPosition = node.PivotPosition;
            Rotation = node.Rotation;
            Size = node.Size;
        }

        public void ApplyTo(SKNode node)
        {
            node.Position = Position;
            node.PivotPosition = PivotPosition;
            node.Rotation = Rotation;
            node.Size = Size;
        }
    }
}
