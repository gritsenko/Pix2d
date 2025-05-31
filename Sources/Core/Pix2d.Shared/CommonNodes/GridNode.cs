using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class GridNode : SKNode
{
    public SKSize CellSize { get; set; } = new SKSize(8, 8);

    private SKPaint _paintBigCells = new SKPaint()
    {
        StrokeWidth = 0,
        Color = SKColors.Gray,
        IsStroke = true
    };

    private SKPaint _paintSmallCells = new SKPaint()
    {
        StrokeWidth = 0,
        Color = SKColor.Parse("#FF909090"),
        IsStroke = true
    };

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        var mpx = vp.Zoom < 4 ? CellSize.Width : CellSize.Width;
        var mpy = vp.Zoom < 4 ? CellSize.Height : CellSize.Height;
        RenderGrid(canvas, GetBoundingBox(), mpx, mpy, _paintSmallCells);
        RenderGrid(canvas, GetBoundingBox(), CellSize.Width * mpx, CellSize.Height * mpy, _paintBigCells);
    }

    public void RenderGrid(SKCanvas canvas, SKRect boudns, float stepx, float stepy, SKPaint paint)
    {
        //if (step < 3)
        //    return;

        for (var y = boudns.Top; y < boudns.Bottom; y += stepy)
            canvas.DrawLine(boudns.Left, y, boudns.Right, y, paint);

        for (var x = boudns.Left; x < boudns.Right; x += stepx)
            canvas.DrawLine(x, boudns.Top, x, boudns.Bottom, paint);
    }
}