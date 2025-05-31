using SkiaSharp;

namespace Pix2d.Abstract.NodeTypes;

public interface IBitmapNode
{
    SKBitmap Bitmap { get; }
}