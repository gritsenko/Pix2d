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
        var mp = vp.Zoom < 4 ? CellSize : 1;

        if (vp.Zoom <= 0.25f)
        {
            mp = CellSize * 2;
        }

        var bounds = vp.GetVisibleArea();

        canvas.DrawLine(0, bounds.Top, 0, bounds.Bottom, _gridPaint);
        canvas.DrawLine(bounds.Left, 0, bounds.Right, 0, _gridPaint);

        //paint.Color = paint.Color.WithAlpha(96);
        //RenderGrid(canvas, bounds, mp, paint);
        //_gridPaint.Color = _gridPaint.Color.WithAlpha(96);
        RenderGrid(canvas, vp.GetVisibleArea(), CellSize * mp, _gridPaint);
    }

    public void RenderGrid(SKCanvas canvas, SKRect boudns, float step, SKPaint paint)
    {
        //if (step < 3)
        //    return;

        for (var y = 0.0f; y < boudns.Bottom; y += step)
            canvas.DrawLine(boudns.Left, y, boudns.Right, y, paint);
        for (var y = 0.0f; y > boudns.Top; y -= step)
            canvas.DrawLine(boudns.Left, y, boudns.Right, y, paint);

        for (var x = 0.0f; x < boudns.Right; x += step)
            canvas.DrawLine(x, boudns.Top, x, boudns.Bottom, paint);
        for (var x = 0.0f; x > boudns.Left; x -= step)
            canvas.DrawLine(x, boudns.Top, x, boudns.Bottom, paint);
    }

}