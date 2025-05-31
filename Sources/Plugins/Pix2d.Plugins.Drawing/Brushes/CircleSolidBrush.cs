using Pix2d.Abstract.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

public class CircleSolidBrush : BasePixelBrush
{
    public override SKBitmap GetPreviewBitmap(float scale)
    {
        var size = (int)scale;
        Preview = CreateBrushBitmap(size, SKColors.White);
        return Preview;
    }

    public override SKBitmap GetBrushBitmap(SKColor color, float scale)
    {
        var bm = base.GetBrushBitmap(color, scale);
        if (bm != null) return _brushBitmap;

        var size = (int)scale;
        _brushBitmap =  CreateBrushBitmap(size, color);
        return _brushBitmap;
    }

    private SKBitmap CreateBrushBitmap(int size, SKColor color)
    {
        var wbm = new SKBitmap(size, size, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        wbm.Clear();
        using (var canvas = new SKCanvas(wbm))
        {
            var r = size / 2;

            using (var paint = canvas.GetSolidFillPaint(color))
                canvas.DrawCircle(new SKPoint(r, r), r, paint);

            canvas.Flush();
        }

        return wbm;
    }

    public override bool Draw(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure, bool ignoreSpacing = false)
    {
        return base.Draw(layer, pos, color, 1, ignoreSpacing);
    }
    public override bool Erase(IDrawingLayer layer, SKPointI pos, double pressure, bool ignoreSpacing)
    {
        return base.Erase(layer, pos, 1, ignoreSpacing);
    }
}