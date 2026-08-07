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

    /// <summary>
    /// The finished region as a binary mask over the whole drawing target (1 = selected), indexed
    /// <c>x + y * width</c>. Null when the selector produced nothing.
    ///
    /// <para>Canvas-space on purpose: it is what lets a fresh marquee be combined with the existing
    /// selection (Shift = add, Ctrl = subtract) without every caller having to reconcile the selectors'
    /// private bounding boxes and offset conventions. Call it only after
    /// <see cref="GetSelectionBitmap"/> — <c>AiPixelSelector</c> computes its mask in there.</para>
    /// </summary>
    byte[]? GetSelectionMask(int width, int height);

    SKPoint Offset { get; }
    void BeginSelection(SKPointI point);
    void AddSelectionPoint(SKPointI point, Action<int, int> plot);
    void ClearSelectionFromBitmap(ref SKBitmap bitmap);
}