using Pix2d.Abstract.Visual;
using Pix2d.VisualStyles;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class RectangleNode : SKNode
{
    public IVisualStyle Style { get; set; } = new SimpleVisualStyle()
    {
        FillColor = SKColors.Black
    };

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        foreach (var paint in Style.GetSkPaints())
            using (paint) canvas.DrawRect(0, 0, this.Size.Width, this.Size.Height, paint);
    }
}