using Pix2d.Plugins.Drawing.Nodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

public class TriangleShapeBuilder : SimpleShapeBuilder
{
    protected override void DrawShape(SKPoint p0, SKPoint p1)
    {
        var dln = ((DrawingLayerNode)DrawingLayer);

        if (dln.AspectSnapper?.IsAspectLocked == true)
        {
            p1 = dln.ProjectAspectPoint(p0, p1, null);
        }

        if (dln.AspectSnapper?.DrawFromCenterLocked == true)
        {
            p0 -= p1 - p0;
        }

        dln.DrawLine(new SKPoint((p0.X + p1.X) / 2, p0.Y), new SKPoint(p0.X, p1.Y));
        dln.DrawLine(new SKPoint(p0.X, p1.Y), new SKPoint(p1.X, p1.Y));
        dln.DrawLine(new SKPoint((p0.X + p1.X) / 2, p0.Y), new SKPoint(p1.X, p1.Y));
    }
}