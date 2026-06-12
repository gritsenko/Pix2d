using Pix2d.Selection;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

/// <summary>
/// Middle resize thumb on the right edge: changes the frame width (the right edge moves, the left edge
/// stays fixed). Vertical drag is ignored unless aspect lock is active, in which case the height follows
/// the width proportionally. Mirrors <see cref="RightBottomResizeThumbSingleNode"/> with the Y axis pinned.
/// </summary>
public class RightResizeThumbSingleNode : ResizeThumbSingleNode
{
    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var frame = selection?.Frame;
        if (frame == null) return;
        var transform = frame.GetGlobalTransform();
        Position = transform.MapPoint(new SKPoint(frame.LocalBounds.Right, frame.LocalBounds.MidY));
    }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
        var d = new SKPoint(delta.X, 0);
        var newSize = CalculateNewSize(initialSize, d, lockAspect);
        var sizeDelta = GetSizeDelta(initialSize, newSize);

        // Left edge stays fixed; with aspect lock the height grows symmetrically around the vertical center.
        var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
        position.Offset(sizeDelta.X / 2, 0);

        var pivotPosition = _initialTargetPivotPosition;
        pivotPosition.Offset(sizeDelta.X / 2, sizeDelta.Y / 2);

        TargetSelection?.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
        TargetSelection?.SetPivotPosition(pivotPosition);
        TargetSelection?.SetSize(newSize);
    }
}
