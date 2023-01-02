using SkiaSharp;

namespace Pix2d.CommonNodes;


public class SpriteNode : BitmapNode
{
    public SpriteNode()
    {
    }

    public SpriteNode(SKSize size)
    {
        Bitmap = new SKBitmap(new SKImageInfo((int) size.Width, (int) size.Height, SKColorType.Bgra8888));
    }
}