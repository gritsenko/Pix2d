using System;
using Pix2d.Abstract.Selection;
using SkiaSharp;
using SkiaNodes.Extensions;
namespace Pix2d.CommonNodes.Controls.Thumbs.Resize
{
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
            
            var newSize = CalculateNewSize(initialSize, delta, lockAspect);
            TargetSelection.SetSize(newSize.Width, newSize.Height);
        }
    }
}