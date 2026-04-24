using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

public class LeftTopResizeThumbSingleNode : ResizeThumbSingleNode
{
    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
            var frame = selection?.Frame;
            if (frame == null) return;
            var transform = frame.GetGlobalTransform();
            Position = transform.MapPoint(frame.LocalBounds.GetLeftTopPoint());
        }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
            var d = new SKPoint(-delta.X, -delta.Y);
            var newSize = CalculateNewSize(initialSize, d, lockAspect);
            var sizeDelta = GetSizeDelta(initialSize, newSize);
            var effectiveDelta = new SKPoint(-sizeDelta.X, -sizeDelta.Y);

            var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
            position.Offset(effectiveDelta.X / 2, effectiveDelta.Y / 2);

            var pivotPosition = _initialTargetPivotPosition;
            pivotPosition.Offset(-effectiveDelta.X / 2, - effectiveDelta.Y / 2);
            
            TargetSelection?.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
            TargetSelection?.SetPivotPosition(pivotPosition);

            TargetSelection?.SetSize(newSize);
        }
}