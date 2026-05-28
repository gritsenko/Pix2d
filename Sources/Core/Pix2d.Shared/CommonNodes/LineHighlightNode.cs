using System;
using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class LineHighlightNode : SKNode, IDisposable
{
    private SKSize _originalSize;
    private SKPath? _localPath;
    private IReadOnlyList<IReadOnlyList<SKPoint>>? _localContours;
    private NodesSelection? TargetSelection { get; set; }

    public LineHighlightNode()
    {
        NodeInvalidated += AdjustToTarget;
    }

    public void SetSelection(NodesSelection? targetSelection, SKPath? selectionPath, IReadOnlyList<IReadOnlyList<SKPoint>>? contours = null)
    {
        _localPath?.Dispose();
        _localPath = null;
        _localContours = null;

        TargetSelection = targetSelection;

        if (selectionPath != null)
        {
            var bounds = GetPathBounds(selectionPath, contours);
            var origin = bounds.Location;

            _originalSize = bounds.Size;
            _localPath = NormalizePath(selectionPath, origin);
            _localContours = NormalizeContours(contours, origin);
        }
        else if (targetSelection?.Frame != null)
        {
            _originalSize = targetSelection.Frame.Size;
        }
        else
        {
            _originalSize = default;
        }

        // Visibility tracks the path: a node selection (rectangle marquee) passes null and falls back to the
        // move thumb's bounding rect; non-rectangular selections (lasso, same-colour) supply the real contour
        // and must render it here.
        IsVisible = selectionPath != null;

        AdjustToTarget(this, EventArgs.Empty);
    }

    /// <summary>
    /// When true, <see cref="AdjustToTarget"/> stops syncing the displayed transform to the frame. The
    /// contour keeps rendering at whatever Size/Position/Rotation it had when the freeze started, so the
    /// marching ants stay still while the user drags the manipulator. The frame still moves underneath
    /// (the bounding-box stroke + lifted bitmap follow the drag); only this contour is decoupled.
    /// <see cref="Pix2d.InteractiveNodes.FrameEditorNode"/> flips the flag on edit-start / edit-complete.
    /// </summary>
    public bool FreezeTransformUpdates { get; set; }

    /// <summary>
    /// Called by <see cref="Pix2d.InteractiveNodes.FrameEditorNode"/> after a transform completes to bring the
    /// contour up to the final pixel-snapped frame state in one step — replaces the per-frame jitter the
    /// freeze suppressed during the drag.
    /// </summary>
    public void SyncTransformToFrame()
    {
        AdjustToTarget(this, EventArgs.Empty);
    }

    private void AdjustToTarget(object? sender, EventArgs e)
    {
        if (TargetSelection?.Frame == null) return;
        if (FreezeTransformUpdates) return;

        var frame = TargetSelection!.Frame!;
        Size = frame.Size;
        Position = frame.Position;
        Rotation = frame.Rotation;
        PivotPosition = frame.PivotPosition;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (_localPath == null) return;

        var sx = _originalSize.Width <= 0 ? 1 : Size.Width / _originalSize.Width;
        var sy = _originalSize.Height <= 0 ? 1 : Size.Height / _originalSize.Height;

        SKPath path;
        if (_localContours != null)
        {
            // After a transform resize, sub-pixel scaling makes the marching ants drift inside physical
            // pixels (the user-visible bug: contour appears squished smaller than the actual fragment).
            // Rebuild from raw contour vertices, snapping each scaled vertex to the nearest pixel boundary
            // so the dashed outline always lands on whole-pixel edges of the displayed fragment.
            path = BuildSnappedPath(_localContours, sx, sy);
        }
        else
        {
            // Fallback for selections without raw contour data (legacy entry points). Math-scale the
            // stored path — no snap, but the bounds still match for purely axis-aligned selections.
            path = new SKPath();
            _localPath.Transform(SKMatrix.CreateScale(sx, sy), path);
        }

        try
        {
            // Two-tone marching ants so the contour stays visible on both light and dark canvases.
            // Path effects MUST be disposed — assigning to paint.PathEffect doesn't transfer ownership,
            // and OnDraw runs every frame so an undisposed dash effect leaks a managed handle per frame.
            var dashLen = SelectionOutlineMetrics.GetDashLengthWorld(vp);
            var strokeWidth = SelectionOutlineMetrics.GetStrokeWidthWorld(vp);
            using var blackPaint = canvas.GetSimpleStrokePaint(strokeWidth, SKColors.Black);
            using var whitePaint = canvas.GetSimpleStrokePaint(strokeWidth, SKColors.White);
            using var blackDash = SKPathEffect.CreateDash([dashLen, dashLen], 0);
            using var whiteDash = SKPathEffect.CreateDash([dashLen, dashLen], dashLen);
            blackPaint.PathEffect = blackDash;
            whitePaint.PathEffect = whiteDash;

            canvas.DrawPath(path, blackPaint);
            canvas.DrawPath(path, whitePaint);
        }
        finally
        {
            path.Dispose();
        }
    }

    private static SKPath? NormalizePath(SKPath? selectionPath, SKPoint origin)
    {
        if (selectionPath == null)
            return null;

        var localPath = new SKPath();
        selectionPath.Transform(SKMatrix.CreateTranslation(-origin.X, -origin.Y), localPath);
        return localPath;
    }

    private static List<List<SKPoint>>? NormalizeContours(IReadOnlyList<IReadOnlyList<SKPoint>>? contours, SKPoint origin)
    {
        if (contours == null)
            return null;

        var normalizedContours = new List<List<SKPoint>>(contours.Count);
        foreach (var contour in contours)
        {
            var normalizedContour = new List<SKPoint>(contour.Count);
            foreach (var point in contour)
            {
                normalizedContour.Add(new SKPoint(point.X - origin.X, point.Y - origin.Y));
            }

            normalizedContours.Add(normalizedContour);
        }

        return normalizedContours;
    }

    private static SKRect GetPathBounds(SKPath selectionPath, IReadOnlyList<IReadOnlyList<SKPoint>>? contours)
    {
        if (contours == null || contours.Count == 0)
            return selectionPath.Bounds;

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        foreach (var contour in contours)
        {
            foreach (var point in contour)
            {
                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
            }
        }

        if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
            return selectionPath.Bounds;

        return new SKRect(minX, minY, maxX, maxY);
    }

    private static SKPath BuildSnappedPath(IReadOnlyList<IReadOnlyList<SKPoint>> contours, float sx, float sy)
    {
        // Contours are normalized to the selection-local coordinate space in SetSelection(), so scaling
        // can happen directly around the local top-left corner without depending on canvas-space offsets.
        SKPoint Transform(SKPoint p) => new(
            MathF.Round(sx * p.X),
            MathF.Round(sy * p.Y));

        var path = new SKPath();
        foreach (var contour in contours)
        {
            if (contour.Count == 0) continue;

            var first = Transform(contour[0]);
            path.MoveTo(first);
            var prev = first;
            for (int i = 1; i < contour.Count; i++)
            {
                var p = Transform(contour[i]);
                if (p == prev) continue; // Skip degenerate zero-length segments produced by snap collapse.
                path.LineTo(p);
                prev = p;
            }
            path.Close();
        }
        return path;
    }

    public void Dispose()
    {
        NodeInvalidated -= AdjustToTarget;
        _localPath?.Dispose();
    }
}