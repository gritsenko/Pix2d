using Pix2d.Drawing.Nodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Drawing.Tools
{
    public class LineShapeBuilder : SimpleShapeBuilder
    {
        protected override void DrawShape(SKPoint p0, SKPoint p1)
        {
            WorkingBitmap.Clear();

            var dln = ((DrawingLayerNode)DrawingLayer);

            if (dln.AspectSnapper?.IsAspectLocked == true)
            {
                p1 = dln.SnapPointToAngleGrid(p0, p1);
            }

            if (dln.AspectSnapper?.DrawFromCenterLocked == true)
            {
                p0 -= p1 - p0;
            }

            dln.DrawLine(p0, p1);
        }
    }
}