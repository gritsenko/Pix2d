using System;
using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public interface IPixelSelector
{
    void FinishSelection(bool highlightSelection);
    SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap);
    SKPath? GetSelectionPath();

    /// <summary>
    /// Raw vertex lists per contour (one list per closed sub-contour). Returned alongside
    /// <see cref="GetSelectionPath"/> so callers can rebuild a snapped/scaled <see cref="SKPath"/> later
    /// (e.g. after a transform resize) without losing per-contour topology.
    /// May be null for selectors that don't produce contours (e.g. rectangle marquee).
    /// </summary>
    List<List<SKPoint>>? GetSelectionContours();

    SKPoint Offset { get; }
    void BeginSelection(SKPointI point);
    void AddSelectionPoint(SKPointI point, Action<int, int> plot);
    void ClearSelectionFromBitmap(ref SKBitmap bitmap);
}