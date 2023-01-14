using Pix2d.Abstract.Selection;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes.Controls.Thumbs.Resize
{
    public class LeftBottomResizeThumbSingleNode : ResizeThumbSingleNode
    {
        protected override void AdjustDimensionsToTargets(NodesSelection selection)
        {
            var frame = selection.Frame;
            var transform = frame.GetGlobalTransform();
            Position = transform.MapPoint(frame.LocalBounds.GetLeftBottomPoint());
        }

        protected override void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect)
        {
            var d = new SKPoint(-delta.X, delta.Y);
            var newSize = CalculateNewSize(initialSize, d, lockAspect);

            var pos = _initialTargetLocalTransform.MapPoint(_initialTargetPos);
            pos.Offset(delta.X, 0);
            TargetSelection.SetPosition(_initialTargetGlobalTransform.MapPoint(pos));

            TargetSelection.SetSize(newSize.Width, newSize.Height);
        }

    }
}