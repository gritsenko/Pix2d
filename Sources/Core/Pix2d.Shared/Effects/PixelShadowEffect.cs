using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class PixelShadowEffect : ISKNodeEffect
{
    private SKImageFilter _shadowFilter;
    private SKSurface _effectRenderCache;
    private SKImageInfo _cacheImageInfo;

    public string Name => "Shadow";
    public EffectType EffectType => EffectType.BackEffect;

    public float Blur { get; set; } = 15;

    public float SigmaX => Blur / 10;
    public float SigmaY => Blur / 10;

    public float DeltaX { get; set; } = 3;

    public float DeltaY { get; set; } = 3;

    public SKColor Color { get; set; } = SKColors.Black;

    public float Opacity { get; set; } = 200;

    public void Render(RenderContext rc, SKSurface source)
    {
        if (_shadowFilter == null) Invalidate();

        using var paint = new SKPaint();
        paint.ImageFilter = _shadowFilter;

        if (_effectRenderCache == null 
            || !rc.ViewPort.Size.Equals(_cacheImageInfo.Size))
        {
            _effectRenderCache?.Dispose();
            _cacheImageInfo = new SKImageInfo((int)rc.ViewPort.Size.Width, (int)rc.ViewPort.Size.Height);
            _effectRenderCache = SKSurface.Create(_cacheImageInfo);
        }

        _effectRenderCache.Canvas.Clear();
        _effectRenderCache.Canvas.DrawSurface(source, 0, 0, paint);
        rc.Canvas.DrawSurface(_effectRenderCache, 0, 0);
    }

    public void Invalidate()
    {
        _shadowFilter = SKImageFilter.CreateDropShadowOnly(DeltaX, DeltaY, SigmaX, SigmaY, Color.WithAlpha((byte)Opacity));
    }

    public ISKNodeEffect Clone()
    {
        return new PixelShadowEffect();
    }
}