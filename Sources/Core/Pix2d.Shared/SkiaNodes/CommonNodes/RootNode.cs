using SkiaNodes.Extensions;
using SkiaSharp;

namespace SkiaNodes
{
    public class RootNode : SKNode
    {
        public SKColor GridColor { get; set; } = SKColors.Gray;

        public override bool ContainsPoint(SKPoint pos)
        {
            return true;
        }

        public bool ShowGrid { get; set; }
        public int CellSize { get; set; } = 8;

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            if (!ShowGrid)
                return;
            using (var paint = canvas.GetSolidFillPaint(GridColor))
            {
                var mp = vp.Zoom < 4 ? CellSize : 1;

                if (vp.Zoom <= 0.25f)
                {
                    mp = CellSize * 2;
                }

                var bounds = vp.GetVisibleArea();

                canvas.DrawLine(0, bounds.Top, 0, bounds.Bottom, paint);
                canvas.DrawLine(bounds.Left, 0, bounds.Right, 0, paint);

                //paint.Color = paint.Color.WithAlpha(96);
                //RenderGrid(canvas, bounds, mp, paint);
                paint.Color = paint.Color.WithAlpha(96);
                RenderGrid(canvas, vp.GetVisibleArea(), CellSize * mp, paint);
            }

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
}