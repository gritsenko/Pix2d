using System;
using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public interface IPixelSelector
{
    void FinishSelection();
    SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap);
    SKPoint Offset { get; }
    void BeginSelection(SKPointI point);
    void AddSelectionPoint(SKPointI point, Action<int, int> plot);
    void ClearSelectionFromBitmap(ref SKBitmap bitmap);
}