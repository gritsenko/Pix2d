#nullable enable
using Pix2d.Selection;
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
    // Single persistent SKPath that we Reset() rather than re-allocate. The previous design replaced
    // _path on every SetRectanglePath / BeginFreeformPath call — those run on every pointer move during
    // a drag and the old path was never disposed, leaking native handles per pointer event. Reusing
    // one path keeps the native object stable across the gesture (no Dispose race with the render
    // thread reading the path concurrently) and avoids allocation churn.
    private readonly SKPath _path = new();
    private bool _hasPath;

    // Outline of the selection that is being added to / subtracted from during a Shift/Ctrl gesture. The
    // press already dropped the live marquee (BeginSelection applies the previous selection), so without
    // this the user would drag the new region against a blank canvas and only see the combined result on
    // release. Static for the whole gesture — it is a snapshot, not something the drag reshapes.
    private SKPath? _basePath;

    public void SetRectanglePath(SKPointI a, SKPointI b)
    {
        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);
        // +1 on the far edge: a selected pixel (x, y) occupies the square [x, x+1] × [y, y+1], so the
        // marquee must close on the outer pixel boundary, not on the pixel's top-left corner.
        _path.Reset();
        _path.AddRect(new SKRect(x1, y1, x2 + 1, y2 + 1));
        _hasPath = true;
    }

    public void BeginFreeformPath(SKPoint start)
    {
        _path.Reset();
        _path.MoveTo(start);
        _hasPath = true;
    }

    public void AddFreeformPoint(SKPoint p)
    {
        if (_hasPath) _path.LineTo(p);
    }

    /// <summary>
    /// Shows the outline of the selection the current gesture combines with. Pass null (or call
    /// <see cref="Clear"/>) to drop it.
    /// </summary>
    public void SetBasePath(SKPath? path) => _basePath = path;

    public void Clear()
    {
        _path.Reset();
        _hasPath = false;
        _basePath = null;
    }

    public override bool ContainsPoint(SKPoint worldPos) => false;

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (!_hasPath && _basePath == null) return;

        // Path effects must be disposed — assigning to paint.PathEffect doesn't transfer ownership,
        // and OnDraw runs every frame during a marquee drag so an undisposed dash effect leaks a
        // managed handle per frame.
        var dashLen = SelectionOutlineMetrics.GetDashLengthWorld(vp);
        var strokeWidth = SelectionOutlineMetrics.GetStrokeWidthWorld(vp);
        using var blackPaint = canvas.GetSimpleStrokePaint(strokeWidth, SKColors.Black);
        using var whitePaint = canvas.GetSimpleStrokePaint(strokeWidth, SKColors.White);
        using var blackDash = SKPathEffect.CreateDash([dashLen, dashLen], 0);
        using var whiteDash = SKPathEffect.CreateDash([dashLen, dashLen], dashLen);
        blackPaint.PathEffect = blackDash;
        whitePaint.PathEffect = whiteDash;

        if (_basePath != null)
        {
            canvas.DrawPath(_basePath, blackPaint);
            canvas.DrawPath(_basePath, whitePaint);
        }

        if (!_hasPath) return;

        canvas.DrawPath(_path, blackPaint);
        canvas.DrawPath(_path, whitePaint);
    }
}
