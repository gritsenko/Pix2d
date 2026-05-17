#nullable enable
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// Vector overlay drawn while the user is actively dragging out a selection (before
/// <see cref="SelectionController.FinishSelection"/> hands off to <see cref="Pix2d.InteractiveNodes.FrameEditorNode"/>).
/// The previous implementation stamped dashed pixels straight into the working bitmap — at high zoom each "ant"
/// looked like a giant coloured square and the marquee boundary disappeared. This node renders the marquee with
/// screen-pixel-relative stroke width so it stays a thin frame at any zoom, and at pixel-edge coordinates so
/// the outline sits between pixels rather than on top of them.
/// </summary>
internal sealed class SelectionMarqueeOverlayNode : SKNode
{
    private SKPath? _path;

    public void SetRectanglePath(SKPointI a, SKPointI b)
    {
        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);
        // +1 on the far edge: a selected pixel (x, y) occupies the square [x, x+1] × [y, y+1], so the
        // marquee must close on the outer pixel boundary, not on the pixel's top-left corner.
        var path = new SKPath();
        path.AddRect(new SKRect(x1, y1, x2 + 1, y2 + 1));
        _path = path;
    }

    public void BeginFreeformPath(SKPoint start)
    {
        var path = new SKPath();
        path.MoveTo(start);
        _path = path;
    }

    public void AddFreeformPoint(SKPoint p)
    {
        _path?.LineTo(p);
    }

    public void Clear()
    {
        _path = null;
    }

    public override bool ContainsPoint(SKPoint worldPos) => false;

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (_path == null) return;

        var dashLen = vp.PixelsToWorld(4);
        using var blackPaint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1.5f), SKColors.Black);
        using var whitePaint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1.5f), SKColors.White);
        blackPaint.PathEffect = SKPathEffect.CreateDash([dashLen, dashLen], 0);
        whitePaint.PathEffect = SKPathEffect.CreateDash([dashLen, dashLen], dashLen);
        canvas.DrawPath(_path, blackPaint);
        canvas.DrawPath(_path, whitePaint);
    }
}
