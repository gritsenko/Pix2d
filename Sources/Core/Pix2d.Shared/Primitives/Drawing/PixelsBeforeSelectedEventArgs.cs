using System;
using SkiaSharp;

namespace Pix2d.Primitives.Drawing;

public class PixelsBeforeSelectedEventArgs : EventArgs
{
    public SKBitmap SelectionBitmap { get; }

    public PixelsBeforeSelectedEventArgs(SKBitmap selectionBitmap)
    {
        SelectionBitmap = selectionBitmap;
    }
}