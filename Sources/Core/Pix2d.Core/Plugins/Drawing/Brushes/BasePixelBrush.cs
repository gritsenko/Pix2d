using Pix2d.Abstract.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

/// <summary>Backdrop the brush stroke preview is rendered over; cycled by the preview's corner button.</summary>
public enum BrushPreviewBackground
{
    DarkChecker,
    White,
    LightChecker,
}

public abstract class BasePixelBrush : IPixelBrush, IDisposable
{
    protected SKBitmap? Preview;
    protected float _opacity = 1f;
    protected float _scale = 1f;
    protected SKPointI _lastPos;

    /// <summary>
    /// False until the current stroke has placed its first dab. Spacing is measured from the previous dab,
    /// so without this flag the opening dab of a new stroke/shape is measured against the *previous* one's
    /// last dab and gets swallowed whenever the two happen to start close together. Cleared by
    /// <see cref="BeginStroke"/>.
    /// </summary>
    private bool _hasLastPos;

    protected float _cacheSize;
    protected SKColor _cacheColor;
    protected float Spacing { get; set; } = 0.01f;
    public float AbsoluteSpacing { get; set; } = 1;

    protected SKBitmap? _brushBitmap;
    private SKSurface? _surface;

    public int Size => (int) _scale;
    public float Opacity => _opacity;

    /// <inheritdoc />
    /// <remarks>Pixel by default — hard brushes keep the legacy per-dab path. Soft brushes override.</remarks>
    public virtual BrushStrokeStyle StrokeStyle => BrushStrokeStyle.Pixel;

    /// <summary>Marker = even-opacity stroke buffer; Airbrush/Pixel = per-dab build-up.</summary>
    protected bool UsesEvenOpacity => StrokeStyle == BrushStrokeStyle.Marker;

    public bool PressureAffectsSize { get; set; }
    public bool PressureAffectsOpacity { get; set; }

    /// <summary>
    /// Live stylus pressure for the current stamp. The freehand stroke path sets this per pointer event;
    /// every other path leaves it at the default <c>1</c>, so the pressure factors below collapse to 1.
    /// </summary>
    public double CurrentPressure { get; set; } = 1;

    // --- Pressure response shaping -------------------------------------------------------------
    // Raw stylus pressure is a linear [0..1] value, but mapping it straight onto size/opacity makes
    // the light end collapse to nothing (1px at ~0% alpha). These knobs remap it into a usable curve:
    //   1. subtract a small dead zone (drops sensor noise right at first contact),
    //   2. renormalize to [0..1] and raise to a gamma (the non-linear "feel"),
    //   3. lift the result into [min..1] so the lightest real contact is always visible.
    // At full press (p == 1, and for mouse/touch which report 1) every curve returns exactly 1.0, so
    // non-pressure drawing and shapes stay pixel-identical to before.

    /// <summary>Pressure below this is treated as the lightest contact (sensor dead zone). Keep small.</summary>
    public double PressureThreshold { get; set; } = 0.0;

    /// <summary>Smallest fraction of the brush size the lightest touch produces. 0 = taper to 1px.</summary>
    public double MinSizePressure { get; set; } = 0.25;

    /// <summary>Smallest fraction of opacity the lightest touch produces. The fix for "light touch is invisible".</summary>
    public double MinOpacityPressure { get; set; } = 0.35;

    /// <summary>Size curve exponent. 1 = linear; &gt;1 gives finer control over thin strokes; &lt;1 ramps up fast.</summary>
    public double SizePressureGamma { get; set; } = 1.0;

    /// <summary>Opacity curve exponent. 1 = linear; &lt;1 makes light strokes read sooner; &gt;1 darkens only on press.</summary>
    public double OpacityPressureGamma { get; set; } = 1.0;

    private double SizePressureFactor =>
        PressureAffectsSize ? ShapePressure(CurrentPressure, MinSizePressure, SizePressureGamma) : 1.0;

    private double OpacityPressureFactor =>
        PressureAffectsOpacity ? ShapePressure(CurrentPressure, MinOpacityPressure, OpacityPressureGamma) : 1.0;

    private double ShapePressure(double raw, double min, double gamma)
    {
        // Drop the dead zone, then renormalize what's left to [0..1].
        var t = Math.Clamp((raw - PressureThreshold) / (1.0 - PressureThreshold), 0.0, 1.0);
        // Non-linear feel, then lift into [min..1] so the lightest contact never fully disappears.
        return min + (1.0 - min) * Math.Pow(t, gamma);
    }

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

    /// <summary>
    /// A fresh, independent brush of this kind for throwaway rendering — see
    /// <see cref="RenderStrokePreview"/>, which mutates pressure and the stamp cache and therefore must never
    /// run on the singleton the canvas draws with. The caller owns the result and disposes it.
    ///
    /// <para>A procedural brush is fully described by its type, so the default reflects one up. A brush
    /// carrying per-instance data (<see cref="ImageStampBrush"/> and its captured bitmap) has no parameterless
    /// constructor and <b>must</b> override this — reflection on such a type throws
    /// <see cref="MissingMethodException"/>. Overrides must not hand ownership of shared state to the copy,
    /// since the caller disposes it.</para>
    /// </summary>
    public virtual BasePixelBrush CreatePreviewInstance() => (BasePixelBrush)Activator.CreateInstance(GetType())!;

    public SKSurface? GetPreviewSurface(SKColor color, float scale)
    {
        var bm = GetBrushBitmap(color, scale);
        if (bm == null)
            return null;

        // Dispose the previously cached surface before allocating a new one — this is called on every
        // color/size change, so overwriting the field leaked a native SKSurface each time.
        _surface?.Dispose();
        _surface = null;

        // SKSurface.Create returns null (not throws) when it can't allocate — e.g. under memory pressure
        // on mobile, or a zero-sized image info. Guard both so we return null instead of NRE-ing on .Canvas.
        var info = new SKImageInfo(Math.Max(1, bm.Width), Math.Max(1, bm.Height), bm.ColorType);
        _surface = SKSurface.Create(info);
        if (_surface == null)
            return null;

        using var canvas = _surface.Canvas;
        canvas.DrawBitmap(bm, 0, 0);
        return _surface;
    }

    public virtual SKBitmap? GetBrushBitmap(SKColor color, float scale)
    {
        if (color.Equals(_cacheColor) && Math.Abs(scale - _cacheSize) < 0.1)
            return _brushBitmap;

        _cacheColor = color;
        _cacheSize = scale;

        // Release the previously cached stamp before the concrete override allocates a new one — otherwise
        // every size/color change orphans a native SKBitmap (amplified by the stroke preview, which sweeps
        // pressure across its whole range and thus changes the requested size on almost every stamp).
        _brushBitmap?.Dispose();
        _brushBitmap = null;

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


    /// <inheritdoc />
    public virtual void BeginStroke() => _hasLastPos = false;

    public virtual bool Draw(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure,
        bool ignoreSpacing = false)
    {
        //ignoreSpacing = true;
        if (ignoreSpacing)
        {
            DrawCore(layer, pos, color, pressure);
            return true;
        }

        // The opening dab of a stroke has nothing to be spaced from, so it always lands.
        if (!_hasLastPos || pos.DistanceTo(_lastPos) >= AbsoluteSpacing)
        {
            _lastPos = pos;
            _hasLastPos = true;
            DrawCore(layer, pos, color, pressure);
            return true;
        }

        return false;
    }

    public virtual bool Erase(IDrawingLayer layer, SKPointI pos, double pressure, bool ignoreSpacing)
    {
        if (!ignoreSpacing)
        {
            var tooClose = _hasLastPos && pos.DistanceTo(_lastPos) < AbsoluteSpacing;
            _lastPos = pos;
            _hasLastPos = true;
            if (tooClose)
                return false;
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

        if (UsesEvenOpacity)
        {
            // Marker: lay the dabs into the stroke buffer at FULL strength so their opaque cores saturate the
            // spine to a uniform alpha (no per-dab build-up), then let the drawing layer composite the whole
            // stroke once at _opacity. Pressure still tapers the ends via the per-dab alpha here; the brush
            // opacity is applied later, at the single buffer→layer composite.
            layer.DrawWithBitmap(bm, destRect, SKBlendMode.SrcOver, (float)OpacityPressureFactor);
            return;
        }

        // Pixel / Airbrush: deposit at the per-dab opacity so overlapping dabs build up density.
        layer.DrawWithBitmap(bm, destRect, SKBlendMode.SrcOver, (float)(_opacity * OpacityPressureFactor));
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

    /// <summary>
    /// Renders a representative sample stroke into a fresh bitmap for the brush-settings panel. The stroke
    /// is laid out at 100% scale (1 brush pixel = 1 bitmap pixel) along a sine "swoosh" and stamped with a
    /// thin→thick→thin pressure profile, reusing the exact live-stamp math (<see cref="DrawCore"/>). The
    /// <see cref="PressureAffectsSize"/> / <see cref="PressureAffectsOpacity"/> toggles gate whether that
    /// pressure varies the width / opacity, so the preview reacts to them just like a real stylus stroke.
    /// <para>Mutates <see cref="CurrentPressure"/> and the stamp cache, so call it on a throwaway brush
    /// instance — never the brush the canvas is currently drawing with. <see cref="CreatePreviewInstance"/>
    /// is how you get that instance.</para>
    /// </summary>
    public SKBitmap RenderStrokePreview(int width, int height, SKColor color, BrushPreviewBackground background)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var bitmap = new SKBitmap(width, height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        bitmap.Clear();

        using var canvas = new SKCanvas(bitmap);

        // Backdrop so opacity / spray fade reads correctly behind the stroke. Drawn in Skia (not an Avalonia
        // ImageBrush) so the checker tiles stay square and evenly spaced for any box aspect ratio.
        DrawPreviewBackground(canvas, width, height, background);

        // Keep the widest (full-pressure) stamp inside the box and shrink the wave amplitude for large
        // brushes so the stroke stays centered instead of clipping against the top/bottom edges.
        var radius = Math.Max(1f, _scale * 0.5f);
        var marginX = radius + 2f;
        var centerY = height * 0.5f;
        var amplitude = Math.Max(0f, Math.Min(height * 0.3f, (height - _scale) * 0.5f - 2f));

        var x0 = marginX;
        var x1 = Math.Max(marginX + 1f, width - marginX);

        var lastStampPos = SKPointI.Empty;
        var hasStamped = false;

        // Marker brushes paint as an even-opacity stroke: accumulate the whole sample stroke at full strength
        // into a separate layer (union), then lay it down once at _opacity — mirrors the live canvas stamping
        // so the preview reads identically. Pixel/airbrush brushes stamp straight onto the canvas as before
        // (airbrush builds up just like the live stroke).
        SKBitmap? strokeLayer = null;
        SKCanvas? strokeCanvas = null;
        if (UsesEvenOpacity)
        {
            strokeLayer = new SKBitmap(width, height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
            strokeLayer.Clear();
            strokeCanvas = new SKCanvas(strokeLayer);
        }
        var stampCanvas = strokeCanvas ?? canvas;

        // Sample densely; the brush's own spacing (AbsoluteSpacing) thins the stamps exactly like a live stroke.
        var steps = Math.Max(2, (int)(x1 - x0));
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var x = x0 + (x1 - x0) * t;
            var y = centerY + amplitude * (float)Math.Sin(t * Math.PI * 2);

            // Classic thin→thick→thin profile; the 0.15 floor keeps the tapered ends faintly visible.
            CurrentPressure = 0.15 + 0.85 * Math.Sin(t * Math.PI);

            var pos = new SKPointI((int)Math.Round(x), (int)Math.Round(y));
            if (hasStamped && pos.DistanceTo(lastStampPos) < AbsoluteSpacing)
                continue;

            lastStampPos = pos;
            hasStamped = true;
            StampPreview(stampCanvas, pos, color);
        }

        if (strokeLayer != null)
        {
            strokeCanvas!.Flush();
            using var paint = new SKPaint
            {
                Color = SKColor.Empty.WithAlpha((byte)Math.Clamp(_opacity * 255.0, 0, 255))
            };
            canvas.DrawBitmap(strokeLayer, 0, 0, paint);
            strokeCanvas.Dispose();
            strokeLayer.Dispose();
        }

        canvas.Flush();
        CurrentPressure = 1;
        return bitmap;
    }

    // Mirror of DrawCore's stamp step, compositing onto an offscreen preview canvas instead of a drawing layer.
    private void StampPreview(SKCanvas canvas, SKPointI pos, SKColor color)
    {
        var sizeFactor = SizePressureFactor;
        // Round to whole pixels: concrete brushes rasterize at integer sizes anyway, and this lets the
        // size-keyed stamp cache hit across equal pressure steps instead of re-rasterizing every stamp.
        var bm = GetBrushBitmap(color, MathF.Round(EffectiveScale(_scale, sizeFactor)));
        if (bm == null)
            return;

        var destRect = GetRect(pos - StampOffset(bm, sizeFactor), new SKSize(bm.Width, bm.Height));

        // Match DrawCore: marker lays dabs at full strength (bar pressure) into a stroke layer that the
        // caller composites once at _opacity; pixel/airbrush deposit at the per-dab opacity directly.
        var opacity = UsesEvenOpacity ? OpacityPressureFactor : _opacity * OpacityPressureFactor;
        var alpha = (byte)Math.Clamp(opacity * 255.0, 0, 255);
        using var paint = new SKPaint
        {
            Color = SKColor.Empty.WithAlpha(alpha),
            BlendMode = SKBlendMode.SrcOver,
        };
        canvas.DrawBitmap(bm, destRect, paint);
    }

    // 2x2 transparency-checker tiles. Dark sits on the popup's dark surface; light matches the canvas checker.
    private static readonly SKBitmap PreviewCheckerDark = new(2, 2, Pix2DAppSettings.ColorType, SKAlphaType.Premul)
    {
        Pixels =
        [
            new SKColor(0xff3C3C3C), new SKColor(0xff2E2E2E),
            new SKColor(0xff2E2E2E), new SKColor(0xff3C3C3C),
        ]
    };

    private static readonly SKBitmap PreviewCheckerLight = new(2, 2, Pix2DAppSettings.ColorType, SKAlphaType.Premul)
    {
        Pixels =
        [
            new SKColor(0xffffffff), new SKColor(0xffd2d2d2),
            new SKColor(0xffd2d2d2), new SKColor(0xffffffff),
        ]
    };

    private static void DrawPreviewBackground(SKCanvas canvas, int width, int height, BrushPreviewBackground background)
    {
        var rect = new SKRect(0, 0, width, height);

        if (background == BrushPreviewBackground.White)
        {
            using var fill = new SKPaint { Color = SKColors.White };
            canvas.DrawRect(rect, fill);
            return;
        }

        var pattern = background == BrushPreviewBackground.LightChecker ? PreviewCheckerLight : PreviewCheckerDark;
        // Scale the 2x2 pattern so each cell is 8px square (16px period), same technique as the canvas checker.
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateBitmap(pattern, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat,
                SKMatrix.CreateScale(8f, 8f))
        };
        canvas.DrawRect(rect, paint);
    }

    public virtual void Dispose()
    {
        _brushBitmap?.Dispose();
        _brushBitmap = null;
        Preview?.Dispose();
        Preview = null;
        _surface?.Dispose();
        _surface = null;
    }
}