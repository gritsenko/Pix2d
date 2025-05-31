using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class GrayscaleEffect : ISKNodeEffect
{
    private readonly SKColorFilter _filter = SKColorFilter.CreateColorMatrix([
        0.21f, 0.72f, 0.07f, 0, 0,
        0.21f, 0.72f, 0.07f, 0, 0,
        0.21f, 0.72f, 0.07f, 0, 0,
        0,     0,     0,     1, 0
    ]);

    public string Name => "Grayscale";
    public EffectType EffectType { get; } = EffectType.ReplaceEffect;

    public void Render(RenderContext rc, SKSurface source)
    {
        using var paint = new SKPaint();
        paint.ColorFilter = _filter;
        rc.Canvas.DrawSurface(source, 0, 0, paint);
    }

    public void Invalidate()
    {
    }
}