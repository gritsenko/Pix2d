using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Common;
using Pix2d.Plugins.Drawing.Common.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// Encapsulates bitmap stroke rasterization for <see cref="DrawingLayerNode"/> while leaving
/// target/bitmap lifecycle and input orchestration on the node.
///
/// <para><b>Two coordinate spaces meet here, and mixing them up is silent on an artboard at the world
/// origin and fatal anywhere else.</b> The freehand pipeline feeds <b>world</b> points (the router passes
/// <c>Pointer.WorldPosition</c> straight through), so <see cref="DrawStroke"/> / <see cref="DrawPointStroke"/>
/// / <see cref="EraseStroke"/> map them through the inverse global transform. The shape builders feed
/// <b>layer-local</b> points — they resolve the pointer with <c>GetPosition(drawingLayer)</c> before handing
/// it over — so <see cref="DrawLine"/> / <see cref="DrawRect"/> / <see cref="DrawEllipse"/> must not map
/// again. Every entry point below says which space it takes; keep new ones equally explicit.</para>
/// </summary>
internal sealed class StrokeRenderer
{
    private readonly IStrokeRendererHost _host;

    public StrokeRenderer(IStrokeRendererHost host)
    {
        _host = host;
    }

    /// <summary>Shape outline, in <b>layer-local</b> coordinates.</summary>
    public void DrawLine(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
        => DrawStrokeLocal(p0, p1, brush, color, opacity, scale);

    /// <summary>Shape outline, in <b>layer-local</b> coordinates.</summary>
    public void DrawRect(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        var p00 = p0;
        var p01 = new SKPoint(p1.X, p0.Y);
        var p02 = p1;
        var p03 = new SKPoint(p0.X, p1.Y);

        DrawStrokeLocal(p00, p01, brush, color, opacity, scale);
        DrawStrokeLocal(p01, p02, brush, color, opacity, scale);
        DrawStrokeLocal(p02, p03, brush, color, opacity, scale);
        DrawStrokeLocal(p00, p03, brush, color, opacity, scale);
    }

    /// <summary>Shape outline, in <b>layer-local</b> coordinates.</summary>
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

        // Collect the outline first instead of stamping as we go. The midpoint loops below emit four
        // mirrored points per step, so in emission order consecutive dabs jump across the whole ellipse —
        // and since a brush measures its dab spacing from the previous dab, that made spacing (and an image
        // stamp's phase) meaningless on an oval. Walking the collected ring in perimeter order makes a
        // spaced / stamp brush step around the outline exactly the way it steps along a line. For a brush
        // whose spacing admits every point (the 1px default) the painted pixels are unchanged — same point
        // set, order-independent — and the de-dupe only removes double-stamps at the quadrant seams, which
        // used to darken those four pixels for a semi-transparent brush.
        var outline = new List<SKPointI>();
        var seen = new HashSet<SKPointI>();

        void Plot(double xp, double yp)
        {
            var p = new SKPointI((int)xp, (int)yp);
            if (seen.Add(p))
                outline.Add(p);
        }

        void Plot4(double xp, double yp)
        {
            Plot(xc + xp + dx, yc + yp + dy);
            Plot(xc - xp, yc + yp + dy);
            Plot(xc + xp + dx, yc - yp);
            Plot(xc - xp, yc - yp);
        }

        var x = 0;
        var y = height;
        var sigma = 2 * b2 + a2 * (1 - 2 * height);
        for (; b2 * x <= a2 * y; x++)
        {
            Plot4(x, y);
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
            Plot4(x, y);
            if (sigma >= 0)
            {
                sigma += fb2 * (1 - x);
                x--;
            }

            sigma += a2 * ((4 * y) + 6);
        }

        outline.Sort((l, r) => Math.Atan2(l.Y - yc, l.X - xc).CompareTo(Math.Atan2(r.Y - yc, r.X - xc)));

        foreach (var p in outline)
            DrawPoint(brush, p, color, pressure: 1);
    }

    /// <summary>Single freehand dab, in <b>world</b> coordinates.</summary>
    public void DrawPointStroke(SKPoint p0, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        var pivot = SKPoint.Empty;
        var point = invertedTransform.MapPoint(p0 + pivot).ToSkPointI();
        DrawPoint(brush, point, color, (int)scale, true);
    }

    /// <summary>Freehand stroke segment, in <b>world</b> coordinates.</summary>
    public void DrawStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        if (float.IsNaN(p1.Length) || float.IsNaN(p0.Length))
            return;

        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        DrawStrokeLocal(invertedTransform.MapPoint(p0), invertedTransform.MapPoint(p1), brush, color, opacity, scale);
    }

    /// <summary>
    /// The actual segment rasterizer, in <b>layer-local</b> coordinates. Shapes call this directly: their
    /// points are already layer-local, and mapping them through the inverse global transform a second time
    /// shifted the whole outline by the artboard's world position — invisible on an artboard at the origin,
    /// and off-canvas (so nothing drawn at all) on every other one.
    /// </summary>
    public void DrawStrokeLocal(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        if (float.IsNaN(p1.Length) || float.IsNaN(p0.Length))
            return;

        var p00 = p0.ToSkPointI();
        var p01 = p1.ToSkPointI();

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

    /// <summary>Freehand erase segment, in <b>world</b> coordinates.</summary>
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

    /// <remarks>
    /// <paramref name="pressure"/> is fed straight to <see cref="IPixelBrush.Draw"/>, where it <b>multiplies
    /// the stamp size</b> — it is not a scale factor for the point. Every rasterizer here passes 1; the
    /// ellipse used to pass <c>brush.Size</c>, which stamped a size-5 brush at 25px and off-center.
    /// </remarks>
    private void DrawPoint(IPixelBrush brush, SKPointI p, SKColor color, int pressure, bool ignoreSpacing = false)
    {
        var isDrawn = brush.Draw(_host, p, color, pressure, ignoreSpacing);

        if (isDrawn && (_host.MirrorX || _host.MirrorY))
        {
            var ox = _host.MirrorX ? brush.PixelOffset.X * 2 : 0;
            var oy = _host.MirrorY ? brush.PixelOffset.Y * 2 : 0;
            brush.Draw(_host, GetMirroredPoint(p, new SKPointI(ox, oy), _host.Brush.Size), color, pressure, true);
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