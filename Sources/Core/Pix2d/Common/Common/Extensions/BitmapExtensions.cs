using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Pix2d.UI.Common.Extensions;

public static class BitmapExtensions
{
    public static Bitmap ToBitmap(this SKBitmap bitmap)
    {
        if (bitmap == null)
            return null;

        var result = new Bitmap(
            PixelFormat.Rgba8888,
            AlphaFormat.Premul,
            bitmap.GetPixels(),
            new PixelSize(bitmap.Width, bitmap.Height),
            new Vector(96.0, 96.0),
            bitmap.RowBytes);

        return result;
    }

    public static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        var info = new SKImageInfo(bitmap.PixelSize.Width, bitmap.PixelSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var result = new SKBitmap(info);
        var dest = result.GetPixels(out var len);
        var stride = bitmap.PixelSize.Width * 4;

        bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), dest, (int)len, stride);

        return result;
    }

    public static ImageBrush ToBrush(this SKBitmap bitmap)
    {
        return new ImageBrush(bitmap.ToBitmap());
    }
}