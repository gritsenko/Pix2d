using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

public abstract class SimpleShapeBuilder : ShapeBuilderBase
{
    protected override void OnPointAdded(SKPoint point, int pointsCount)
    {
        if (pointsCount > 1)
        {
            DrawShape(Points[0].ToSkPointI(), Points[1].ToSkPointI());
            Finish();
        }
    }

    public override void SetNextPointPreview(SKPoint previewPoint)
    {
        var pointsCount = Points.Count;
        if (pointsCount == 1)
        {
            DrawShape(Points[0].ToSkPointI(), previewPoint.ToSkPointI());
        }
    }

    protected abstract void DrawShape(SKPoint p0, SKPoint p1);
}