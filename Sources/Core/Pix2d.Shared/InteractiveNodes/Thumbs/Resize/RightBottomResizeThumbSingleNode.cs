using Pix2d.Abstract.Selection;
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
            var frame = selection.Frame;
            var transform = frame.GetGlobalTransform();
            Position = transform.MapPoint(frame.LocalBounds.GetRightBottomPoint());
        }

    protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
            if(delta == SKPoint.Empty)
                return;
            
            var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
            position.Offset(delta.X / 2, delta.Y / 2);

            var pivotPosition = _initialTargetPivotPosition;
            pivotPosition.Offset(delta.X / 2,  delta.Y / 2);
            
            TargetSelection.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
            TargetSelection.SetPivotPosition(pivotPosition);
            
            var newSize = CalculateNewSize(initialSize, delta, lockAspect);
            TargetSelection.SetSize(newSize.Width, newSize.Height);
        }
}