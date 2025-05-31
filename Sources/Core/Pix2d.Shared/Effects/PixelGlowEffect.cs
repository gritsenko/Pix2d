using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class PixelGlowEffect : ISKNodeEffect
{
    private SKImageFilter _imageFilter;
    public string Name => "Glow";
    public EffectType EffectType { get; } = EffectType.BackEffect;

    public float Blur { get; set; } = 15;

    public float Radius { get; set; } = 1;

    public int Opacity { get; set; } = 255;

    public void Render(RenderContext rc, SKSurface source)
    {
        if(_imageFilter == null)
            Invalidate();

        using var paint = new SKPaint();
        paint.Color = SKColors.Black.WithAlpha((byte)Opacity);
        paint.ImageFilter = _imageFilter;
        rc.Canvas.DrawSurface(source, 0, 0, paint);
    }

    public void Invalidate()
    {
        var dilate = SKImageFilter.CreateDilate(Radius, Radius);
        var sigma = Blur / 10;
        _imageFilter = SKImageFilter.CreateBlur(sigma, sigma, dilate);
    }
}