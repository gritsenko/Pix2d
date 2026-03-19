using Avalonia.Platform;
using SkiaSharp;

namespace Pix2d.Common.Extensions;

public static class BitmapExtensions
{
    public static Bitmap? ToBitmap(this SKBitmap bitmap)
    {
        if (bitmap == null)
            return null;
        var result = new Bitmap(
            PixelFormats.Rgba8888,
            AlphaFormat.Premul,
            bitmap.GetPixels(),
            new PixelSize(bitmap.Width, bitmap.Height),
            new Vector(96.0, 96.0),
            bitmap.RowBytes);

        return result;
    }

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