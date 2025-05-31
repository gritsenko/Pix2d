using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class ColorOverlayEffect : ISKNodeEffect
{
    public string Name => "Color overlay";
    public EffectType EffectType { get; } = EffectType.OverlayEffect;

    public float Opacity { get; set; } = 255;

    public SKColor Color { get; set; } = SKColors.Red;

    public void Render(RenderContext rc, SKSurface source)
    {
        rc.Canvas.DrawColor(Color.WithAlpha((byte)Opacity), SKBlendMode.SrcATop);
    }

    public void Invalidate()
    {
    }
}