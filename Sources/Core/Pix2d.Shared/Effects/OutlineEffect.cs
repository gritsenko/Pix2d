using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class OutlineEffect : ISKNodeEffect
{
    private SKImageFilter _imageFilter;
    public string Name => "Outline";
    public EffectType EffectType => EffectType.OverlayEffect;

    public float Radius { get; set; } = 3;

    public SKColor Color { get; set; } = SKColors.White;

    public void Render(RenderContext rc, SKSurface source)
    {
        if(_imageFilter == null) Invalidate();

        using var paint = new SKPaint();
        paint.ImageFilter = _imageFilter;
        rc.Canvas.DrawSurface(source, 0, 0, paint);
    }

    public void Invalidate()
    {
        var colorFilter = SKColorFilter.CreateBlendMode(Color, SKBlendMode.SrcIn);
        var color = SKImageFilter.CreateColorFilter(colorFilter);
        _imageFilter = SKImageFilter.CreateDilate((float)Math.Round(Radius), (float)Math.Round(Radius), color);
    }
}