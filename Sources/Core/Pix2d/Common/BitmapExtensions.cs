using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Pix2d.Common;

public static class BitmapExtensions
{
    public static Bitmap ToBitmap(this SKBitmap bitmap)
    {
        var result = new Bitmap(
            PixelFormat.Bgra8888,
            AlphaFormat.Premul,
            bitmap.GetPixels(),
            new PixelSize(bitmap.Width, bitmap.Height),
            new Vector(96.0, 96.0),
            bitmap.RowBytes);

        return result;
    }

    public static ImageBrush ToBrush(this SKBitmap bitmap)
    {
        return new ImageBrush(bitmap.ToBitmap());
    }
}