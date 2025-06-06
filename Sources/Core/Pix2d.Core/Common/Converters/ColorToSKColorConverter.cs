﻿#nullable enable
using System.Globalization;
using Pix2d.Common.Extensions;
using SkiaSharp;

namespace Pix2d.Common.Converters;

public class ColorToSKColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, targetType);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, targetType);
    }

    object Convert(object value, Type targetType)
    {
        return targetType == typeof(SKColor) ? ToSkColor(value) : ToColor(value);
    }

    SKColor ToSkColor(object value)
    {
        var col = value is Color ? (Color)value : default;

        if (col == default)
            return SKColor.Empty;

        return col.ToSKColor();
    }

    Color ToColor(object value)
    {
        var col = value is SKColor ? (SKColor)value : default;

        if (col == default)
            return default;

        return col.ToColor();
    }
}