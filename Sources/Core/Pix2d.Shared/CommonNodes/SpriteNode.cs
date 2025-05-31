using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;


public class SpriteNode : BitmapNode
{
    public SpriteNode()
    {
    }

    public SpriteNode(SKSize size)
    {
        Bitmap = new SKBitmap(new SKImageInfo((int) size.Width, (int) size.Height, Pix2DAppSettings.ColorType));
    }

    public void RenderToCanvas(SKCanvas canvas, ViewPort vp)
    {
        OnDraw(canvas, vp);
    }
}