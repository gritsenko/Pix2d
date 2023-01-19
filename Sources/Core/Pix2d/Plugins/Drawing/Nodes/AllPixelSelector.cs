using System;
using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Drawing.Nodes
{
    public class AllPixelSelector : IPixelSelector
    {
        public void FinishSelection()
        {
        }

        public SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
        {
            return sourceBitmap;
        }

        public SKPoint Offset { get; }
        public void BeginSelection(SKPointI point)
        {
            throw new NotImplementedException();
        }

        public void AddSelectionPoint(SKPointI point, Action<int, int> plot)
        {
            throw new NotImplementedException();
        }

        public void ClearSelectionFromBitmap(ref SKBitmap bitmap)
        {
            bitmap.Erase(SKColor.Empty);
            bitmap.NotifyPixelsChanged();
        }
    }
}