#nullable enable
using SkiaNodes.Extensions;
using SkiaSharp;

namespace SkiaNodes;

public class RootNode : SKNode
{
    private SKPaint? _gridPaint;
    public SKColor GridColor { get; set; } = SKColor.Parse("#2D2D2F");

    public override bool ContainsPoint(SKPoint pos)
    {
        return true;
    }

    public bool ShowGrid { get; set; }
    public int CellSize { get; set; } = 4;

    protected internal override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (!ShowGrid)
            return;

        _gridPaint ??= canvas.GetSolidFillPaint(GridColor);
        var cellSize = GridUtils.CalculateAdaptiveStep(vp.DpiEffectiveZoom);
        RenderGrid(canvas, vp.GetVisibleArea(), cellSize, _gridPaint);
    }

    public void RenderGrid(SKCanvas canvas, SKRect bounds, float step, SKPaint paint)
    {
        float startX = MathF.Floor(bounds.Left / step) * step;
        float startY = MathF.Floor(bounds.Top / step) * step;

        // Pre-calculate the number of vertical lines needed
        int verticalLineCount = (int)MathF.Ceiling((bounds.Right - startX) / step);

        // Collect all vertical line points
        var verticalPoints = new List<SKPoint>(verticalLineCount * 2);
        for (int i = 0; i < verticalLineCount; i++)
        {
            float x = startX + (i * step);
            verticalPoints.Add(new SKPoint(x, bounds.Top));
            verticalPoints.Add(new SKPoint(x, bounds.Bottom));
        }

        // Pre-calculate the number of horizontal lines needed
        int horizontalLineCount = (int)MathF.Ceiling((bounds.Bottom - startY) / step);

        // Collect all horizontal line points
        var horizontalPoints = new List<SKPoint>(horizontalLineCount * 2);
        for (int i = 0; i < horizontalLineCount; i++)
        {
            float y = startY + (i * step);
            horizontalPoints.Add(new SKPoint(bounds.Left, y));
            horizontalPoints.Add(new SKPoint(bounds.Right, y));
        }

        // Draw all vertical lines in one call
        if (verticalPoints.Count > 0)
            canvas.DrawPoints(SKPointMode.Lines, verticalPoints.ToArray(), paint);

        // Draw all horizontal lines in one call
        if (horizontalPoints.Count > 0)
            canvas.DrawPoints(SKPointMode.Lines, horizontalPoints.ToArray(), paint);
    }
}