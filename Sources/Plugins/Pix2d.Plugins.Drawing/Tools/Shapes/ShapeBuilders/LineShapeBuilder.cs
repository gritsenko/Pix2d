using Pix2d.Plugins.Drawing.Nodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

public class LineShapeBuilder : SimpleShapeBuilder
{
    protected override void DrawShape(SKPoint p0, SKPoint p1)
    {
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