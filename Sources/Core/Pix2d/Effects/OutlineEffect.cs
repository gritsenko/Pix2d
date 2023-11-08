using System.Collections.Generic;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Effects;

public class OutlineEffect : BaseEffect
{
    private float _radius = 3;
    private SKColor _color = SKColors.White;
    
    private SKImageFilter _imageFilter;
    public override string Name => "Outline";
    public override EffectType EffectType { get; } = EffectType.BackEffect;

    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value; 
            OnSettingsChanged();
        }
    }

    public SKColor Color
    {
        get => _color;
        set
        {
            _color = value;
            OnSettingsChanged();
        }
    }

    public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
    {
        using var paint = new SKPaint();
        paint.ImageFilter = _imageFilter;
        var rect = new SKRect(0, 0, vp.Size.Width, vp.Size.Height);
        canvas.DrawBitmap(renderResultBitmap, rect, paint);
    }

    private void UpdateFilter()
    {
        var colorFilter = SKColorFilter.CreateBlendMode(_color, SKBlendMode.SrcIn);
        var color = SKImageFilter.CreateColorFilter(colorFilter);
        _imageFilter = SKImageFilter.CreateDilate((float)Math.Round(_radius), (float)Math.Round(_radius), color);
    }

    protected override void OnSettingsChanged()
    {
        UpdateFilter();
        base.OnSettingsChanged();
    }

    public override ISKNodeEffect Clone()
    {
        return new OutlineEffect();
    }

    public OutlineEffect()
    {
        Order = 0;
        UpdateFilter();
    }
}