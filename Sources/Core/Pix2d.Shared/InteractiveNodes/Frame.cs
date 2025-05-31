using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

public class Frame : SKNode
{
    public SKColor StrokeColor { get; set; } = SKColors.Gray;
    public float StrokeThickness { get; set; } = 1f;
    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(StrokeThickness), StrokeColor);
        canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
    }

    public void SetSecondCornerPosition(SKPoint pos)
    {
        var delta = (pos - this.Position);
        Size = new SKSize(delta);
    }
}