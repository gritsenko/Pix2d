using System;
using SkiaSharp;

namespace Pix2d.Abstract.UI;

public interface ISKBitmapObservable
{
    event EventHandler BitmapChanged;
    SKBitmap Bitmap { get; }

}