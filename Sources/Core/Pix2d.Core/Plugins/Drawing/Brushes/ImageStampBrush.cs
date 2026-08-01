using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

/// <summary>
/// A brush stamped from a bitmap captured off the canvas (<c>IDrawingService.CreateBrushPresetFromSelection</c>)
/// instead of a procedural shape. Unlike Square/Circle/Spray/Marker, one instance belongs to exactly one preset
/// — it is not one of <c>DrawingService.Brushes</c>' shared singletons, so <see cref="BrushKeys"/> (built for
/// resolving a handful of known singleton types by CLR type) does not cover it; the persisted
/// <c>BrushPresetData</c> embeds this brush's source bitmap directly instead of a type key.
///
/// <para>Not wired into any dispose-on-delete path: deleting a preset only drops it from the presets list, and
/// an active <c>CurrentBrushSettings</c> clone can still be sharing this exact instance mid-stroke. The captured
/// bitmap is reclaimed by the ordinary <c>SKBitmap</c> finalizer instead, the same as every other orphaned
/// bitmap in this codebase.</para>
/// </summary>
public class ImageStampBrush : BasePixelBrush
{
    private readonly SKBitmap _source;

    /// <summary>
    /// False for the throwaway copies <see cref="CreatePreviewInstance"/> hands out: they read the preset's
    /// bitmap but must not dispose it, since the caller disposes the copy while the preset keeps drawing.
    /// </summary>
    private readonly bool _ownsSource;

    /// <summary>
    /// True reproduces the captured pixels as-is (a decal, ignoring the paint color); false treats the capture
    /// as an alpha mask recolored with the paint color, like every procedural brush already behaves.
    /// </summary>
    public bool UseOriginalColors { get; }

    public ImageStampBrush(SKBitmap source, bool useOriginalColors)
        : this(source, useOriginalColors, ownsSource: true)
    {
    }

    private ImageStampBrush(SKBitmap source, bool useOriginalColors, bool ownsSource)
    {
        _source = source;
        UseOriginalColors = useOriginalColors;
        _ownsSource = ownsSource;
    }

    /// <summary>
    /// A stamp has no parameterless constructor, so the reflection-based default would throw. The copy shares
    /// the captured bitmap rather than duplicating it (the preview re-renders on every slider tick) and is
    /// marked non-owning so disposing it leaves the preset's source intact.
    /// </summary>
    public override BasePixelBrush CreatePreviewInstance()
        => new ImageStampBrush(_source, UseOriginalColors, ownsSource: false);

    /// <summary>The captured pixels, at their native (possibly downscaled-on-capture) resolution — read by
    /// <c>DrawingService.PersistUserPresets</c> to re-encode this preset for storage.</summary>
    public SKBitmap SourceBitmap => _source;

    /// <summary>
    /// The stamp's native aspect ratio is kept: <paramref name="scale"/> is the target length of the LONGER
    /// side (matching every procedural brush, where Scale is a literal pixel side length), the shorter side
    /// follows proportionally so a wide or tall selection doesn't get squashed into a square.
    /// </summary>
    private SKSizeI GetTargetSize(float scale)
    {
        var longSide = Math.Max(1, (int)Math.Round(scale));
        var nativeLong = Math.Max(1, Math.Max(_source.Width, _source.Height));
        var factor = longSide / (float)nativeLong;

        var w = Math.Max(1, (int)Math.Round(_source.Width * factor));
        var h = Math.Max(1, (int)Math.Round(_source.Height * factor));
        return new SKSizeI(w, h);
    }

    /// <summary>Resamples the source to <paramref name="size"/> with nearest-neighbor filtering, so a resized
    /// stamp stays pixel-crisp instead of blurring — the same sampling <c>BitmapNode.Resize</c> uses.</summary>
    private SKBitmap Resample(SKSizeI size)
    {
        if (size.Width == _source.Width && size.Height == _source.Height)
            return _source.Copy();

        return _source.Resize(size, new SKSamplingOptions(SKFilterMode.Nearest)) ?? _source.Copy();
    }

    /// <summary>Recolors by the source's alpha only: every non-transparent captured pixel becomes
    /// <paramref name="color"/> at that pixel's original alpha — the same "shape mask tinted by the paint
    /// color" behavior every procedural brush has.</summary>
    private static SKBitmap Recolor(SKBitmap shaped, SKColor color)
    {
        var srcPixels = shaped.Pixels;
        var outPixels = new SKColor[srcPixels.Length];
        for (var i = 0; i < srcPixels.Length; i++)
        {
            var alpha = srcPixels[i].Alpha;
            outPixels[i] = alpha == 0 ? SKColor.Empty : color.WithAlpha(alpha);
        }

        return new SKBitmap(shaped.Width, shaped.Height, Pix2DAppSettings.ColorType, SKAlphaType.Premul)
        {
            Pixels = outPixels
        };
    }

    public override SKBitmap GetPreviewBitmap(float scale)
    {
        using var resized = Resample(GetTargetSize(scale));
        Preview = UseOriginalColors ? resized.Copy() : Recolor(resized, SKColors.White);
        return Preview;
    }

    public override SKBitmap? GetBrushBitmap(SKColor color, float scale)
    {
        var bm = base.GetBrushBitmap(color, scale);
        if (bm != null) return _brushBitmap;

        using var resized = Resample(GetTargetSize(scale));
        _brushBitmap = UseOriginalColors ? resized.Copy() : Recolor(resized, color);
        return _brushBitmap;
    }

    public override void Dispose()
    {
        if (_ownsSource)
            _source.Dispose();

        base.Dispose();
    }
}
