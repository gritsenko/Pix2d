using Pix2d.Abstract.Selection;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes.Controls.Thumbs.Resize
{
    public class RightTopResizeThumbSingleNode : ResizeThumbSingleNode
    {
        protected override void AdjustDimensionsToTargets(NodesSelection selection)
        {
            var frame = selection.Frame;
            var transform = frame.GetGlobalTransform();
            Position = transform.MapPoint(frame.LocalBounds.GetRightTopPoint());
        }

        protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
        {
            var d = new SKPoint(delta.X, -delta.Y);
            var newSize = CalculateNewSize(initialSize, d, lockAspect);

            var position = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
            position.Offset(delta.X / 2, delta.Y / 2);

            var pivotPosition = _initialTargetPivotPosition;
            pivotPosition.Offset(delta.X / 2,  -delta.Y / 2);
            
            TargetSelection.SetPosition(_initialTargetGlobalTransform.MapPoint(position));
            TargetSelection.SetPivotPosition(pivotPosition);

            TargetSelection.SetSize(newSize.Width, newSize.Height);
        }
    }
}