using System;
using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class LineHighlightNode : SKNode, IDisposable
{
    private SKPoint _offset;
    private SKSize _originalSize;
    private SKPath? Path { get; set; }
    private IReadOnlyList<IReadOnlyList<SKPoint>>? _contours;
    private NodesSelection? TargetSelection { get; set; }

    public LineHighlightNode()
    {
        Path = new SKPath();
        NodeInvalidated += AdjustToTarget;
    }

    public void SetSelection(NodesSelection? targetSelection, SKPath? selectionPath, IReadOnlyList<IReadOnlyList<SKPoint>>? contours = null)
    {
        Path = selectionPath;
        _contours = contours;
        if (targetSelection?.Frame != null)
        {
            TargetSelection = targetSelection;
            _offset = targetSelection.Frame!.PivotPosition - targetSelection.Frame!.Position;
            _originalSize = targetSelection.Frame!.Size;
        }
        else
        {
            TargetSelection = null;
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
        PivotPosition = frame.PivotPosition - _offset;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (Path == null) return;

        var sx = Size.Width / _originalSize.Width;
        var sy = Size.Height / _originalSize.Height;

        SKPath path;
        if (_contours != null)
        {
            // After a transform resize, sub-pixel scaling makes the marching ants drift inside physical
            // pixels (the user-visible bug: contour appears squished smaller than the actual fragment).
            // Rebuild from raw contour vertices, snapping each scaled vertex to the nearest pixel boundary
            // so the dashed outline always lands on whole-pixel edges of the displayed fragment.
            path = BuildSnappedPath(_contours, sx, sy, _offset);
        }
        else
        {
            // Fallback for selections without raw contour data (legacy entry points). Math-scale the
            // stored path — no snap, but the bounds still match for purely axis-aligned selections.
            path = new SKPath();
            var transformMatrix = SKMatrix.CreateTranslation(_offset.X, _offset.Y)
                .PostConcat(SKMatrix.CreateScale(sx, sy))
                .PostConcat(SKMatrix.CreateTranslation(-_offset.X, -_offset.Y));
            Path.Transform(transformMatrix, path);
        }

        // Two-tone marching ants so the contour stays visible on both light and dark canvases.
        var dashLen = vp.PixelsToWorld(4);
        using var blackPaint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1.5f), SKColors.Black);
        using var whitePaint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1.5f), SKColors.White);
        blackPaint.PathEffect = SKPathEffect.CreateDash([dashLen, dashLen], 0);
        whitePaint.PathEffect = SKPathEffect.CreateDash([dashLen, dashLen], dashLen);

        canvas.DrawPath(path, blackPaint);
        canvas.DrawPath(path, whitePaint);
    }

    private static SKPath BuildSnappedPath(IReadOnlyList<IReadOnlyList<SKPoint>> contours, float sx, float sy, SKPoint offset)
    {
        // Must match the matrix-form transform in the fallback branch:
        //   M = translate(offset).PostConcat(scale).PostConcat(translate(-offset))
        //     = T(-offset) * S * T(offset)
        //   M(p) = S * (p + offset) - offset
        // The "anchor" of the scale is therefore at -offset (= selection top-left in local coords), not at
        // +offset. Getting this wrong dumps every vertex near the origin and the contour visually vanishes.
        // After scaling, snap to integer (= pixel boundary) so the dashed outline lands on whole-pixel edges.
        SKPoint Transform(SKPoint p) => new(
            MathF.Round(sx * (p.X + offset.X) - offset.X),
            MathF.Round(sy * (p.Y + offset.Y) - offset.Y));

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
    }
}