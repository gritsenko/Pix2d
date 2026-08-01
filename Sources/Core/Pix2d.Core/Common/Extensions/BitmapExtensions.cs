using Avalonia.Platform;
using SkiaSharp;

namespace Pix2d.Common.Extensions;

public static class BitmapExtensions
{
    /// <summary>
    /// Copies an <see cref="SKBitmap"/> into an Avalonia <see cref="Bitmap"/>.
    ///
    /// <para>The pixel buffer is handed over RAW, so the declared format must be the bitmap's ACTUAL one.
    /// This used to hardcode <c>Rgba8888</c> — correct for every bitmap the app allocates itself
    /// (<c>Pix2DAppSettings.ColorType</c>), wrong for anything that came back from a decoder: SkiaSharp
    /// decodes into the platform's native N32 type, which is <c>Bgra8888</c> on Windows/Android, so those
    /// bitmaps rendered with red and blue exchanged (a saved image-stamp brush preset previewing its red
    /// pixels as blue after a restart).</para>
    /// </summary>
    public static Bitmap? ToBitmap(this SKBitmap bitmap)
    {
        if (bitmap == null)
            return null;

        var format = GetAvaloniaPixelFormat(bitmap.ColorType);
        if (format != null)
            return CopyToBitmap(bitmap, format.Value, GetAlphaFormat(bitmap.AlphaType));

        // A color type with no byte-compatible Avalonia format (F16, Gray8, Argb4444, …): pay one
        // conversion into the app's own type rather than reinterpreting the bytes.
        using var converted = bitmap.Copy(Pix2DAppSettings.ColorType);
        return converted == null
            ? null
            : CopyToBitmap(converted, PixelFormats.Rgba8888, GetAlphaFormat(converted.AlphaType));
    }

    private static Bitmap CopyToBitmap(SKBitmap bitmap, PixelFormat format, AlphaFormat alphaFormat) =>
        new(format,
            alphaFormat,
            bitmap.GetPixels(),
            new PixelSize(bitmap.Width, bitmap.Height),
            new Vector(96.0, 96.0),
            bitmap.RowBytes);

    /// <summary>Only the color types whose memory layout an Avalonia format matches byte for byte; null
    /// means "convert first" (see <see cref="ToBitmap"/>).</summary>
    private static PixelFormat? GetAvaloniaPixelFormat(SKColorType colorType) => colorType switch
    {
        SKColorType.Rgba8888 => PixelFormats.Rgba8888,
        SKColorType.Bgra8888 => PixelFormats.Bgra8888,
        SKColorType.Rgb565 => PixelFormats.Rgb565,
        _ => null
    };

    private static AlphaFormat GetAlphaFormat(SKAlphaType alphaType) => alphaType switch
    {
        SKAlphaType.Unpremul => AlphaFormat.Unpremul,
        SKAlphaType.Opaque => AlphaFormat.Opaque,
        _ => AlphaFormat.Premul
    };

    public static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        var format = bitmap.Format ?? PixelFormats.Bgra8888;
        var alphaFormat = bitmap.AlphaFormat ?? AlphaFormat.Premul;

        var skColorType = GetSkColorType(format);
        var bytesPerPixel = format.BitsPerPixel / 8;

        var skAlphaType = alphaFormat switch
        {
            AlphaFormat.Premul => SKAlphaType.Premul,
            AlphaFormat.Unpremul => SKAlphaType.Unpremul,
            AlphaFormat.Opaque => SKAlphaType.Opaque,
            _ => SKAlphaType.Premul
        };

        var sourceInfo = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, skColorType, skAlphaType);
        using var sourceBitmap = new SKBitmap(sourceInfo);
        
        var sourcePixels = sourceBitmap.GetPixels(out var sourceLen);
        var sourceStride = bitmap.PixelSize.Width * bytesPerPixel;
        
        bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), sourcePixels, (int)sourceLen, sourceStride);

        var destInfo = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var destBitmap = new SKBitmap(destInfo);
        
        using var canvas = new SKCanvas(destBitmap);
        using var paint = new SKPaint();
        canvas.DrawBitmap(sourceBitmap, 0, 0, paint);

        return destBitmap.Copy();
    }

    private static SKColorType GetSkColorType(PixelFormat format)
    {
        if (format.Equals(PixelFormats.Rgba8888)) return SKColorType.Rgba8888;
        if (format.Equals(PixelFormats.Bgra8888)) return SKColorType.Bgra8888;
        if (format.Equals(PixelFormats.Rgb565)) return SKColorType.Rgb565;
        if (format.Equals(PixelFormats.Rgb32)) return SKColorType.Rgb888x;
        if (format.Equals(PixelFormats.Bgr32)) return SKColorType.Bgra8888;
        if (format.Equals(PixelFormats.Bgr24)) return SKColorType.Rgb888x;
        if (format.Equals(PixelFormats.Rgb24)) return SKColorType.Rgb888x;
        if (format.Equals(PixelFormats.Gray8)) return SKColorType.Gray8;
        return SKColorType.Bgra8888;
    }

    public static ImageBrush ToBrush(this SKBitmap bitmap)
    {
        return new ImageBrush(bitmap.ToBitmap());
    }
}