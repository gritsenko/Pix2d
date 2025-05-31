using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

public class SprayBrush : BasePixelBrush
{
    public SprayBrush()
    {
        Spacing = 0.1f;
    }

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

        _brushBitmap = new SKBitmap((int)scale, (int)scale);
           
        var colors = new[] {color, color.WithAlpha(0)};
        var colorPos = new float[] {0.5f, 1};
        using (var paint = new SKPaint { IsStroke = false })
        using (var canvas = new SKCanvas(_brushBitmap))
        {
            paint.Shader = SKShader.CreateRadialGradient(CenterPoint, scale / 2f, colors, colorPos, SKShaderTileMode.Clamp);
            canvas.Clear();
            canvas.DrawCircle(CenterPoint, scale / 2 - 0.5f, paint);
        }

        return _brushBitmap;
    }

    private SKBitmap CreateBrushBitmap(int size, SKColor color)
    {
        CalculatePoints(size);
        return GetBrushBitmap(color, size).Copy();
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