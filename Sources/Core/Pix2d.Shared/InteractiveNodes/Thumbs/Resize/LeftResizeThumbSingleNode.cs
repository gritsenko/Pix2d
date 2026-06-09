using Pix2d.Selection;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

/// <summary>
/// Middle resize thumb on the left edge: changes only the frame width (the left edge moves, the right edge
/// stays fixed). Vertical drag is ignored. Mirrors <see cref="LeftTopResizeThumbSingleNode"/> with the Y
/// axis pinned.
/// </summary>
public class LeftResizeThumbSingleNode : ResizeThumbSingleNode
{
    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var frame = selection?.Frame;
        if (frame == null) return;
        var transform = frame.GetGlobalTransform();
        Position = transform.MapPoint(new SKPoint(frame.LocalBounds.Left, frame.LocalBounds.MidY));
    }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
        var d = new SKPoint(-delta.X, 0);
        var newSize = CalculateNewSize(initialSize, d, false);
        var sizeDelta = GetSizeDelta(initialSize, newSize);
        var effectiveDelta = new SKPoint(-sizeDelta.X, 0);

        var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
        position.Offset(effectiveDelta.X / 2, effectiveDelta.Y / 2);

        var pivotPosition = _initialTargetPivotPosition;
        pivotPosition.Offset(-effectiveDelta.X / 2, -effectiveDelta.Y / 2);

        TargetSelection?.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
        TargetSelection?.SetPivotPosition(pivotPosition);
        TargetSelection?.SetSize(newSize);
    }
}
