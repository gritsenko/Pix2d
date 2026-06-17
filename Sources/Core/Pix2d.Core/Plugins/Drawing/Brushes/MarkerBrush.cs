using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Brushes;

/// <summary>
/// Soft round marker / pen. Same streamlined, evenly-spaced stroke as the airbrush, but painted in
/// <see cref="BrushStrokeStyle.Marker"/> mode: the whole stroke is laid down once at the brush opacity, so a
/// single pass reads as one flat, even tone (overlap inside the stroke does not darken). The nib is mostly
/// solid with a thin soft edge so it stays crisp rather than feathery like the airbrush.
/// </summary>
public class MarkerBrush : BasePixelBrush
{
    public override BrushStrokeStyle StrokeStyle => BrushStrokeStyle.Marker;

    public override SKBitmap GetPreviewBitmap(float scale)
    {
        var size = (int)scale;
        Preview = CreateBrushBitmap(size, SKColors.White);
        return Preview;
    }

    public override SKBitmap? GetBrushBitmap(SKColor color, float scale)
    {
        var bm = base.GetBrushBitmap(color, scale);
        if (bm != null) return _brushBitmap;

        var size = Math.Max(1, (int)scale);
        _brushBitmap = new SKBitmap(size, size, Pix2DAppSettings.ColorType, SKAlphaType.Premul);

        // Center on the *current* stamp size so pressure-shrunk stamps stay centered (same reasoning as SprayBrush).
        var center = new SKPoint(size / 2f, size / 2f);
        var radius = Math.Max(0.5f, size / 2f);

        // Mostly-solid nib with a thin (~20%) anti-aliased edge — a firm marker/pen tip, not a feathered spray.
        var colors = new[] { color, color, color.WithAlpha(0) };
        var colorPos = new[] { 0f, 0.8f, 1f };
        using (var paint = new SKPaint { IsStroke = false })
        using (var canvas = new SKCanvas(_brushBitmap))
        {
            paint.Shader = SKShader.CreateRadialGradient(center, radius, colors, colorPos, SKShaderTileMode.Clamp);
            canvas.Clear();
            canvas.DrawCircle(center, radius, paint);
        }

        return _brushBitmap;
    }

    private SKBitmap CreateBrushBitmap(int size, SKColor color)
    {
        CalculatePoints(size);
        return GetBrushBitmap(color, size)!.Copy();
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
