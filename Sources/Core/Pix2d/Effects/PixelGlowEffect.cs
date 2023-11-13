using System.Collections.Generic;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Effects;

public class PixelGlowEffect : BaseEffect
{
    private float _blurAmount = 15;
    private float _radius = 1;
    private int _opacity = 255;
    
    private SKImageFilter _imageFilter;
    public override string Name => "Glow";
    public override EffectType EffectType { get; } = EffectType.BackEffect;

    public float Blur
    {
        get => _blurAmount;
        set
        {
            _blurAmount = value;
            OnSettingsChanged();
        }
    }
    
    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            OnSettingsChanged();
        }
    }

    public int Opacity
    {
        get => _opacity;
        set
        {
            _opacity = value;
            OnSettingsChanged();
        }
    }

    public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
    {
        using var paint = new SKPaint();
        paint.Color = SKColors.Black.WithAlpha((byte)_opacity);
        paint.ImageFilter = _imageFilter;
        var rect = new SKRect(0, 0, vp.Size.Width, vp.Size.Height);
        canvas.DrawBitmap(renderResultBitmap, rect, paint);
    }

    private void UpdateImageFilter()
    {
        var dilate = SKImageFilter.CreateDilate(_radius, _radius);
        var sigma = _blurAmount / 10;
        _imageFilter = SKImageFilter.CreateBlur(sigma, sigma, dilate);
    }

    protected override void OnSettingsChanged()
    {
        UpdateImageFilter();
        base.OnSettingsChanged();
    }

    public override ISKNodeEffect Clone()
    {
        return new PixelGlowEffect();
    }

    public PixelGlowEffect()
    {
        Order = 0;
        UpdateImageFilter();
    }
}