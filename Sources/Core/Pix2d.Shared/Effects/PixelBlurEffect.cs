using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class PixelBlurEffect : ISKNodeEffect
{
    private SKImageFilter _blurFilter;
    private SKSurface _effectRenderCache;
    private SKImageInfo _cacheImageInfo;

    public string Name => "Blur";
    public EffectType EffectType => EffectType.ReplaceEffect;

    public float Blur { get; set; } = 15;

    public float SigmaX => Blur / 10;
    public float SigmaY => Blur / 10;

    public void Render(RenderContext rc, SKSurface source)
    {
        if (_effectRenderCache == null
            || !rc.ViewPort.Size.Equals(_cacheImageInfo.Size))
        {
            _cacheImageInfo = new SKImageInfo((int)rc.ViewPort.Size.Width, (int)rc.ViewPort.Size.Height);
            _effectRenderCache = SKSurface.Create(_cacheImageInfo);
        }

        _effectRenderCache.Canvas.Clear();
        using (var paint = new SKPaint())
        {
            paint.ImageFilter = _blurFilter;
            _effectRenderCache.Canvas.DrawSurface(source, 0, 0, paint);
        }

        rc.Canvas.Clear();
        rc.Canvas.DrawSurface(_effectRenderCache, 0, 0);
    }

    public void Invalidate()
    {
        _blurFilter = SKImageFilter.CreateBlur(SigmaX, SigmaY);
    }
}