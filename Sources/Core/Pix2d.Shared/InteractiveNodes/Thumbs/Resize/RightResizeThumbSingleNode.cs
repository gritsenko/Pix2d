using Pix2d.Selection;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

/// <summary>
/// Middle resize thumb on the right edge: changes only the frame width (the right edge moves, the left edge
/// stays fixed). Vertical drag is ignored. Mirrors <see cref="RightBottomResizeThumbSingleNode"/> with the
/// Y axis pinned.
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
