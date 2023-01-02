using System.Collections.Generic;
using Pix2d.Abstract.Visual;
using SkiaSharp;

namespace Pix2d.Visual
{
    public class SimpleVisualStyle : IVisualStyle
    {
        public SKColor FillColor { get; set; }
        public SKColor StrokeColor { get; set; }
        public float StrokeThickness { get; set; }


        public IEnumerable<SKPaint> GetSkPaints()
        {
            if (FillColor != default(SKColor))
                yield return new SKPaint() {Color = FillColor, IsStroke = false};

            if (StrokeColor != default(SKColor))
                yield return new SKPaint() {Color = FillColor, IsStroke = true, StrokeWidth = StrokeThickness};
        }
    }
}