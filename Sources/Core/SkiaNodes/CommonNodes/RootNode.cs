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

        for (float x = startX; x < bounds.Right; x += step)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, paint);
        }

        for (float y = startY; y < bounds.Bottom; y += step)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, paint);
        }
    }

}