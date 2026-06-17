using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Common;
using Pix2d.Plugins.Drawing.Common.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// Encapsulates bitmap stroke rasterization for <see cref="DrawingLayerNode"/> while leaving
/// target/bitmap lifecycle and input orchestration on the node.
/// </summary>
internal sealed class StrokeRenderer
{
    private readonly IStrokeRendererHost _host;

    public StrokeRenderer(IStrokeRendererHost host)
    {
        _host = host;
    }

    public void DrawLine(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
        => DrawStroke(p0, p1, brush, color, opacity, scale);

    public void DrawRect(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        var p00 = p0;
        var p01 = new SKPoint(p1.X, p0.Y);
        var p02 = p1;
        var p03 = new SKPoint(p0.X, p1.Y);

        DrawStroke(p00, p01, brush, color, opacity, scale);
        DrawStroke(p01, p02, brush, color, opacity, scale);
        DrawStroke(p02, p03, brush, color, opacity, scale);
        DrawStroke(p00, p03, brush, color, opacity, scale);
    }

    public void DrawEllipse(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, bool fromCenter = false)
    {
        var xc = p0.X;
        var yc = p0.Y;

        var w = (int)Math.Abs((p1.X - p0.X));
        var h = (int)Math.Abs((p1.Y - p0.Y));

        var dx = 0;
        var dy = 0;

        var width = w;
        var height = h;

        if (!fromCenter)
        {
            xc = (int)((p0.X + p1.X) * 0.5);
            yc = (int)((p0.Y + p1.Y) * 0.5);

            dx = w % 2 > 0 ? 1 : 0;
            dy = h % 2 > 0 ? 1 : 0;

            width = w / 2;
            height = h / 2;
        }

        if (w < 3 || h < 3)
        {
            DrawRect(p0, p1, brush, color, opacity: 1, scale: 1);
            return;
        }

        var a2 = width * width;
        var b2 = height * height;
        var fa2 = 4 * a2;
        var fb2 = 4 * b2;

        Action<double, double> plot = (xp, yp) => DrawPoint(brush, new SKPointI((int)xp, (int)yp), color, brush.Size, false);
        Action<double, double> plot4 = (xp, yp) =>
        {
            plot(xc + xp + dx, yc + yp + dy);
            plot(xc - xp, yc + yp + dy);
            plot(xc + xp + dx, yc - yp);
            plot(xc - xp, yc - yp);
        };

        var x = 0;
        var y = height;
        var sigma = 2 * b2 + a2 * (1 - 2 * height);
        for (; b2 * x <= a2 * y; x++)
        {
            plot4(x, y);
            if (sigma >= 0)
            {
                sigma += fa2 * (1 - y);
                y--;
            }

            sigma += b2 * ((4 * x) + 6);
        }

        x = width;
        y = 0;
        sigma = 2 * a2 + b2 * (1 - 2 * width);
        for (; a2 * y <= b2 * x; y++)
        {
            plot4(x, y);
            if (sigma >= 0)
            {
                sigma += fb2 * (1 - x);
                x--;
            }

            sigma += a2 * ((4 * y) + 6);
        }
    }

    public void DrawPointStroke(SKPoint p0, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        var pivot = SKPoint.Empty;
        var point = invertedTransform.MapPoint(p0 + pivot).ToSkPointI();
        DrawPoint(brush, point, color, (int)scale, true);
    }

    public void DrawStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        if (float.IsNaN(p1.Length) || float.IsNaN(p0.Length))
            return;

        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        var p00 = invertedTransform.MapPoint(p0).ToSkPointI();
        var p01 = invertedTransform.MapPoint(p1).ToSkPointI();

        Algorithms.LineNotSwapped(p00.X, p00.Y, p01.X, p01.Y, (x, y) =>
        {
            var point = new SKPointI(x, y);
            if (!_host.IsInBounds(point))
                return false;

            DrawPoint(brush, point, color, (int)scale);
            return true;
        });
    }

    public void ErasePoint(IPixelBrush brush, SKPointI p, int scale)
    {
        var isDrawn = brush.Erase(_host, p, scale, true);

        if (isDrawn && (_host.MirrorX || _host.MirrorY))
        {
            var ox = _host.MirrorX ? brush.PixelOffset.X * 2 : 0;
            var oy = _host.MirrorY ? brush.PixelOffset.Y * 2 : 0;
            brush.Erase(_host, GetMirroredPoint(p, new SKPointI(ox, oy), _host.Brush.Size), scale, true);
        }
    }

    public void EraseStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, float opacity)
    {
        var pivot = SKPoint.Empty;
        _host.GetGlobalTransform().TryInvert(out var invertedTransform);

        var p00 = invertedTransform.MapPoint(p0 + pivot);
        var p01 = invertedTransform.MapPoint(p1 + pivot);

        Algorithms.LineNotSwapped((int)p00.X, (int)p00.Y, (int)p01.X, (int)p01.Y, (x, y) =>
        {
            var point = new SKPointI(x, y);
            if (!_host.IsInBounds(point))
                return false;

            ErasePoint(brush, point, 1);
            return true;
        });
    }

    public bool FillRegion(SKPoint origin, SKColor fillColor, float tolerance = 0, SKBlendMode blendMode = SKBlendMode.SrcOver)
    {
        var drawingTarget = _host.DrawingTarget;
        if (drawingTarget == null)
            return false;

        var pivot = SKPoint.Empty;

        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        var origin0 = invertedTransform.MapPoint(origin + pivot);
        var fillOrigin = origin0.ToSkPointI();

        if (!_host.IsInBounds(fillOrigin))
            return false;

        drawingTarget.ModifyBitmap(bitmap => FloodFillBitmap(fillOrigin, fillColor, bitmap, tolerance, blendMode));
        return true;
    }

    public SKPointI GetMirroredPoint(SKPointI p, SKPointI brushOffset = default, int brushSize = default)
    {
        var xx = p.X;
        if (_host.MirrorX)
        {
            xx = (int)(_host.Size.Width - p.X) - brushSize;
        }

        var yy = p.Y;
        if (_host.MirrorY)
        {
            yy = (int)(_host.Size.Height - p.Y) - brushSize;
        }

        if (brushOffset != default)
        {
            xx += _host.MirrorX ? brushOffset.X : -brushOffset.X;
            yy += _host.MirrorY ? brushOffset.Y : -brushOffset.Y;
        }

        return new SKPointI(xx, yy);
    }

    /// <summary>
    /// Stamps the brush once at a layer-space point with spacing already decided by the caller (used by the
    /// smooth soft-brush stroke, which spaces dabs at even sub-pixel intervals itself). Honors mirror just
    /// like the normal stroke path.
    /// </summary>
    public void StampPoint(IPixelBrush brush, SKPointI layerPoint, SKColor color, int scale = 1)
        => DrawPoint(brush, layerPoint, color, scale, ignoreSpacing: true);

    private void DrawPoint(IPixelBrush brush, SKPointI p, SKColor color, int scale, bool ignoreSpacing = false)
    {
        var isDrawn = brush.Draw(_host, p, color, scale, ignoreSpacing);

        if (isDrawn && (_host.MirrorX || _host.MirrorY))
        {
            var ox = _host.MirrorX ? brush.PixelOffset.X * 2 : 0;
            var oy = _host.MirrorY ? brush.PixelOffset.Y * 2 : 0;
            brush.Draw(_host, GetMirroredPoint(p, new SKPointI(ox, oy), _host.Brush.Size), color, scale, true);
        }
    }

    private void FloodFillBitmap(SKPointI origin, SKColor fillColor, SKBitmap bitmap, float tolerance, SKBlendMode blendMode)
    {
        var floodFiller = new FloodFiller(bitmap.Pixels, new SKSizeI(bitmap.Width, bitmap.Height));
        floodFiller.FloodFill(origin, fillColor);

        var data = floodFiller.GetPixelBytes();
        _host.WorkingBitmap.CopyPixelsToBitmap(data);

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { BlendMode = blendMode };
        canvas.DrawBitmap(_host.WorkingBitmap, 0, 0, paint);
    }
}