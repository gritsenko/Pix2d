using Pix2d.Abstract.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

public abstract class BasePixelBrush : IPixelBrush
{
    protected SKBitmap? Preview;
    protected float _opacity = 1f;
    protected float _scale = 1f;
    protected SKPointI _lastPos;
    protected float _cacheSize;
    protected SKColor _cacheColor;
    protected float Spacing { get; set; } = 0.01f;
    public float AbsoluteSpacing { get; set; } = 1;

    protected SKBitmap? _brushBitmap;
    private SKSurface? _surface;

    public int Size => (int) _scale;
    public float Opacity => _opacity;

    public bool PressureAffectsSize { get; set; }
    public bool PressureAffectsOpacity { get; set; }

    /// <summary>
    /// Live stylus pressure for the current stamp. The freehand stroke path sets this per pointer event;
    /// every other path leaves it at the default <c>1</c>, so the pressure factors below collapse to 1.
    /// </summary>
    public double CurrentPressure { get; set; } = 1;

    private double SizePressureFactor => PressureAffectsSize ? CurrentPressure : 1.0;
    private double OpacityPressureFactor => PressureAffectsOpacity ? CurrentPressure : 1.0;

    public SKPointI CenterPoint { get; set; }
    public SKPointI BottomRightPoint { get; set; }

    public SKPointI PixelOffset => CenterPoint;
    
    protected void CalculatePoints(float scale)
    {
        var size = (int)Math.Max(1, scale);
        var ds = (int)(0.5 * size);

        var offset = size % 2 == 0 ? 1 : 0;

        CenterPoint = new SKPointI(ds - offset, ds - offset);
        BottomRightPoint = new SKPointI(size - ds + offset, size - ds + offset);
    }

    public abstract SKBitmap GetPreviewBitmap(float scale);

    public SKSurface GetPreviewSurface(SKColor color, float scale)
    {
        var bm = GetBrushBitmap(color, scale);
        if (bm == null)
            throw new InvalidOperationException("Brush bitmap could not be created");
        _surface = SKSurface.Create(new SKImageInfo(bm.Width, bm.Height, bm.ColorType));
        using var canvas = _surface.Canvas;
        canvas.DrawBitmap(bm,0,0);
        return _surface;
    }

    public virtual SKBitmap? GetBrushBitmap(SKColor color, float scale)
    {
        if (color.Equals(_cacheColor) && Math.Abs(scale - _cacheSize) < 0.1)
            return _brushBitmap;

        _cacheColor = color;
        _cacheSize = scale;

        return null;
    }

    public virtual Task InitBrush(float scale, float opacity, float spacing)
    {
        _scale = scale;
        _opacity = opacity;
        Spacing = spacing;
        CalculatePoints(scale);
        AbsoluteSpacing = Spacing;
        return Task.CompletedTask;
    }


    public virtual bool Draw(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure,
        bool ignoreSpacing = false)
    {
        //ignoreSpacing = true;
        if (ignoreSpacing)
        {
            DrawCore(layer, pos, color, pressure);
            return true;
        }

        var dst = pos.DistanceTo(_lastPos);
        if (dst >= AbsoluteSpacing)
        {
            //Debug.WriteLine(dst); 
            _lastPos = pos;
            DrawCore(layer, pos, color, pressure); 
            return true;
        }

        return false;
    }

    public virtual bool Erase(IDrawingLayer layer, SKPointI pos, double pressure, bool ignoreSpacing)
    {
        if (!ignoreSpacing)
        {
            if (pos.DistanceTo(_lastPos) < AbsoluteSpacing)
            {
                _lastPos = pos;
                return false;
            }
            _lastPos = pos;
        }

        EraseCore(layer, pos, pressure);
        return true;
    }

    protected virtual void EraseCore(IDrawingLayer layer, SKPointI pos, double pressure)
    {
        var sizeFactor = SizePressureFactor;
        var bm = GetBrushBitmap(SKColors.White, EffectiveScale(_scale, sizeFactor));
        if (bm == null)
            return;
        var destRect = GetRect(pos - StampOffset(bm, sizeFactor), new SKSize(bm.Width, bm.Height));
        layer.DrawWithBitmap(bm, destRect, SKBlendMode.DstOut, (float)(_opacity * pressure * OpacityPressureFactor));
    }

    protected virtual void DrawCore(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure)
    {
        var sizeFactor = SizePressureFactor;
        var bm = GetBrushBitmap(color, EffectiveScale((float)(_scale * pressure), sizeFactor));
        if (bm == null)
            return;
        var destRect = GetRect(pos - StampOffset(bm, sizeFactor), new SKSize(bm.Width, bm.Height));
        var composMode = SKBlendMode.SrcOver;

        layer.DrawWithBitmap(bm, destRect, composMode, (float)(_opacity * OpacityPressureFactor));
    }

    // Pressure-scaled stamp size, never below 1px (a 0-sized brush bitmap would crash the SKBitmap ctor).
    private static float EffectiveScale(float scale, double sizeFactor) => Math.Max(1f, (float)(scale * sizeFactor));

    // When pressure changes the size, recenter on the actual (smaller/larger) bitmap; otherwise keep the
    // brush's precomputed CenterPoint so non-pressure strokes and shapes are pixel-identical to before.
    private SKPointI StampOffset(SKBitmap bm, double sizeFactor)
        => sizeFactor == 1.0 ? CenterPoint : new SKPointI(bm.Width / 2, bm.Height / 2);

    private SKRect GetRect(SKPointI pos, SKSize size)
    {
        return new SKRect(pos.X, pos.Y, pos.X + size.Width, pos.Y + size.Height);
    }
}