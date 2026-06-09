using Pix2d.Selection;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

/// <summary>
/// Middle resize thumb on the bottom edge: changes only the frame height (the bottom edge moves, the top
/// edge stays fixed). Horizontal drag is ignored. Mirrors <see cref="RightBottomResizeThumbSingleNode"/>
/// with the X axis pinned.
/// </summary>
public class BottomResizeThumbSingleNode : ResizeThumbSingleNode
{
    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var frame = selection?.Frame;
        if (frame == null) return;
        var transform = frame.GetGlobalTransform();
        Position = transform.MapPoint(new SKPoint(frame.LocalBounds.MidX, frame.LocalBounds.Bottom));
    }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
        var d = new SKPoint(0, delta.Y);
        var newSize = CalculateNewSize(initialSize, d, false);
        var effectiveDelta = GetSizeDelta(initialSize, newSize);

        var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
        position.Offset(effectiveDelta.X / 2, effectiveDelta.Y / 2);

        var pivotPosition = _initialTargetPivotPosition;
        pivotPosition.Offset(effectiveDelta.X / 2, effectiveDelta.Y / 2);

        TargetSelection?.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
        TargetSelection?.SetPivotPosition(pivotPosition);
        TargetSelection?.SetSize(newSize);
    }
}
