using SkiaSharp;

namespace Pix2d.Primitives.Drawing;

public class PixelsBeforeSelectedEventArgs(SKBitmap selectionBitmap) : EventArgs
{
    public SKBitmap SelectionBitmap { get; } = selectionBitmap;
}