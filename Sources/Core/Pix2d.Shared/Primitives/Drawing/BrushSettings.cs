using Pix2d.Abstract.Drawing;

namespace Pix2d.Primitives.Drawing
{
    public class BrushSettings
    {
        public IPixelBrush Brush { get; set; }
        public float Scale { get; set; }
        public float Opacity { get; set; }
        public float Spacing { get; set; } = 1;

        public BrushSettings Clone()
        {
            return new BrushSettings()
            {
                Brush = this.Brush,
                Scale = this.Scale,
                Opacity = this.Opacity
            };
        }

        public async void InitBrush()
        {
            await Brush.InitBrush(Scale, Opacity, Spacing);
        }

        protected bool Equals(BrushSettings other)
        {
            return Equals(Brush, other.Brush) && Scale.Equals(other.Scale) && Opacity.Equals(other.Opacity) && Spacing.Equals(other.Spacing);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BrushSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Brush != null ? Brush.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Scale.GetHashCode();
                hashCode = (hashCode * 397) ^ Opacity.GetHashCode();
                hashCode = (hashCode * 397) ^ Spacing.GetHashCode();
                return hashCode;
            }
        }
    }
}