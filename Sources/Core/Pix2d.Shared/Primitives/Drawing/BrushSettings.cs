using Pix2d.Abstract.Drawing;

namespace Pix2d.Primitives.Drawing;

public class BrushSettings
{
    public IPixelBrush? Brush { get; set; }
    public float Scale { get; set; }
    public float Opacity { get; set; }
    public float Spacing { get; set; } = 1;

    /// <summary>When enabled, stylus pen pressure scales the brush size while drawing.</summary>
    public bool PressureAffectsSize { get; set; }

    /// <summary>When enabled, stylus pen pressure scales the brush opacity while drawing.</summary>
    public bool PressureAffectsOpacity { get; set; }

    /// <summary>
    /// True for a preset the user saved (persisted in <c>AppSettings.UserBrushPresets</c>), false for one of
    /// the built-ins <c>DrawingService.InitBrushSettings</c> creates. Only user presets can be deleted.
    ///
    /// <para>Deliberately excluded from <see cref="Equals(object?)"/>/<see cref="GetHashCode"/>: equality here
    /// means "draws identically", which is what the settings view's no-op check and the save-time dedupe both
    /// rely on. Where the preset came from is not part of that.</para>
    /// </summary>
    public bool IsUserPreset { get; set; }

    public BrushSettings Clone()
    {
            return new BrushSettings()
            {
                Brush = this.Brush,
                Scale = this.Scale,
                Opacity = this.Opacity,
                Spacing = this.Spacing,
                PressureAffectsSize = this.PressureAffectsSize,
                PressureAffectsOpacity = this.PressureAffectsOpacity,
                IsUserPreset = this.IsUserPreset,
            };
        }

    public async void InitBrush()
    {
            if (Brush != null)
            {
                Brush.PressureAffectsSize = PressureAffectsSize;
                Brush.PressureAffectsOpacity = PressureAffectsOpacity;
                await Brush.InitBrush(Scale, Opacity, Spacing);
            }
        }

    protected bool Equals(BrushSettings other)
    {
            return Equals(Brush, other.Brush) && Scale.Equals(other.Scale) && Opacity.Equals(other.Opacity) && Spacing.Equals(other.Spacing)
                   && PressureAffectsSize == other.PressureAffectsSize && PressureAffectsOpacity == other.PressureAffectsOpacity;
        }

    public override bool Equals(object? obj)
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
                hashCode = (hashCode * 397) ^ PressureAffectsSize.GetHashCode();
                hashCode = (hashCode * 397) ^ PressureAffectsOpacity.GetHashCode();
                return hashCode;
            }
        }
}