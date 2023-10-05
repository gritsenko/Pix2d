using Avalonia.Media;
using SkiaSharp;

namespace Pix2d.Common;

public static class ColorExtensions
{
    public static Color ToColor(this SKColor skColor)
    {
        return new Color(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
    }
    public static Brush ToBrush(this SKColor skColor)
    {
        return skColor.ToColor().ToBrush();
    }

    public static SKColor ToSKColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    public static Color ToColor(this string hexString)
    {
        return Color.Parse(hexString);
    }
}