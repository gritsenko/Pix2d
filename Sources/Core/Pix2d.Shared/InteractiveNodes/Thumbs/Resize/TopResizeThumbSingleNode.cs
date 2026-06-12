using Pix2d.Selection;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

/// <summary>
/// Middle resize thumb on the top edge: changes the frame height (the top edge moves, the bottom
/// edge stays fixed). Horizontal drag is ignored unless aspect lock is active, in which case the width
/// follows the height proportionally. Mirrors <see cref="LeftTopResizeThumbSingleNode"/> with the
/// X axis pinned.
/// </summary>
public class TopResizeThumbSingleNode : ResizeThumbSingleNode
{
    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var frame = selection?.Frame;
        if (frame == null) return;
        var transform = frame.GetGlobalTransform();
        Position = transform.MapPoint(new SKPoint(frame.LocalBounds.MidX, frame.LocalBounds.Top));
    }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
        var d = new SKPoint(0, -delta.Y);
        var newSize = CalculateNewSize(initialSize, d, lockAspect);
        var sizeDelta = GetSizeDelta(initialSize, newSize);

        // Bottom edge stays fixed; with aspect lock the width grows symmetrically around the horizontal center.
        var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
        position.Offset(0, -sizeDelta.Y / 2);

        var pivotPosition = _initialTargetPivotPosition;
        pivotPosition.Offset(sizeDelta.X / 2, sizeDelta.Y / 2);

        TargetSelection?.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
        TargetSelection?.SetPivotPosition(pivotPosition);
        TargetSelection?.SetSize(newSize);
    }
}
