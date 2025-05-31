#nullable enable
using System.Globalization;
using Pix2d.Common.Extensions;
using SkiaSharp;

namespace Pix2d.Common.Converters;

public class SKBitmapToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is SKBitmap skBitmap ? skBitmap.ToBrush() : default;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}