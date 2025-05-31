using System.Collections.Generic;
using SkiaSharp;

namespace Pix2d.Abstract.Visual;

public interface IVisualStyle
{
    IEnumerable<SKPaint> GetSkPaints();
}