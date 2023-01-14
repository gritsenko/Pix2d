using Pix2d.Abstract.Drawing;
using Pix2d.Drawing.Nodes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Drawing.Tools
{
    public class RectangleShapeBuilder : SimpleShapeBuilder
    {
        protected override void DrawShape(SKPoint p0, SKPoint p1)
        {
            WorkingBitmap.Clear();
            var dln = ((DrawingLayerNode) DrawingLayer);

            if (dln.AspectSnapper?.IsAspectLocked == true)
            {
                p1 = dln.ProjectAspectPoint(p0, p1, null);
            }

            if (dln.AspectSnapper?.DrawFromCenterLocked == true)
            {
                p0 -= p1 - p0;
            }


            dln.DrawRect(p0, p1);
        }
    }
}