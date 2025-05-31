using SkiaSharp;

namespace Pix2d.Abstract.Import;

public class LayerFrameInfo
{
    public Func<SKBitmap> BitmapProviderFunc { get; set; }
}