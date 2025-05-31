using SkiaSharp;

namespace SkiaNodes.Extensions;

public static class SKCanvasExtensions
{

    public static SKPaint GetSimpleStrokePaint(this SKCanvas canvas, float thickness, SKColor color)
    {
            return new SKPaint()
            {
                StrokeWidth = thickness,
                Color = color,
                IsStroke = true
            };
        }
    public static SKPaint GetSolidFillPaint(this SKCanvas canvas, SKColor color)
    {
            return new SKPaint()
            {
                Color = color,
                IsStroke = false
            };
        }
}