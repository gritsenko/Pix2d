using Pix2d.Abstract.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.PixelSelectors;

public class AllPixelSelector : IPixelSelector
{
    public void FinishSelection(bool highlightSelection)
    {
    }

    public SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
    {
        return sourceBitmap.Copy();
    }

    public SKPath? GetSelectionPath() => null;

    public List<List<SKPoint>>? GetSelectionContours() => null;

    public byte[]? GetSelectionMask(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return null;

        var mask = new byte[width * height];
        mask.AsSpan().Fill(1);
        return mask;
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