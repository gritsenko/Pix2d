using Pix2d.Primitives;
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
        // A frame's bitmap is what the drawing pipeline writes into; a 0x0 one makes every stroke on
        // this frame throw out of BitmapNode.EnsureBitmap. Clamp here as well as at the sprite level,
        // since layers can be added with an explicit size of their own. See CanvasSize.
        size = CanvasSize.Sanitize(size);
        Bitmap = new SKBitmap(new SKImageInfo((int) size.Width, (int) size.Height, Pix2DAppSettings.ColorType));
    }

    public void RenderToCanvas(SKCanvas canvas, ViewPort vp)
    {
        OnDraw(canvas, vp);
    }
}