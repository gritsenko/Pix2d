using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

public class SquareSolidBrush : BasePixelBrush
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
        _brushBitmap = CreateBrushBitmap(size, color);
        return _brushBitmap;
    }

    private SKBitmap CreateBrushBitmap(int size, SKColor color)
    {
        var wbm = new SKBitmap(size, size, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        wbm.Erase(color);
        return wbm;
    }

}