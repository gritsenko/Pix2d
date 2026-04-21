using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

public class RightBottomResizeThumbSingleNode : ResizeThumbSingleNode
{
    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
            // if (IsDragging)
            //     return;
            //
            var frame = selection?.Frame;
            if (frame == null) return;
            var transform = frame.GetGlobalTransform();
            Position = transform.MapPoint(frame.LocalBounds.GetRightBottomPoint());
        }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
            if(delta == SKPoint.Empty)
                return;
            var newSize = CalculateNewSize(initialSize, delta, lockAspect);
            var effectiveDelta = GetSizeDelta(initialSize, newSize);
            
            var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
            position.Offset(effectiveDelta.X / 2, effectiveDelta.Y / 2);

            var pivotPosition = _initialTargetPivotPosition;
            pivotPosition.Offset(effectiveDelta.X / 2,  effectiveDelta.Y / 2);
            
            TargetSelection?.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
            TargetSelection?.SetPivotPosition(pivotPosition);

            TargetSelection?.SetSize(newSize.Width, newSize.Height);
        }
}