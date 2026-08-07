using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Common.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.PixelSelectors;

/// <summary>
/// Selects every non-transparent pixel of a mask bitmap — the "load layer transparency as a
/// selection" gesture (Ctrl+click on a layer thumbnail).
///
/// The mask comes from a bitmap handed in by the caller, not from the drawing target, so the
/// silhouette of one layer can be used to select on another (which is the point of the gesture:
/// mask a shading layer to the character underneath it). The mask must be the same size as the
/// drawing target — <see cref="Nodes.SelectionController.SelectOpaquePixels"/> enforces that.
///
/// Shaped like <see cref="SameColorSelector"/>'s whole-layer pass — a full-bitmap mask plus a
/// traced contour — but keyed on alpha instead of a seed color, so there is no seed point and
/// <see cref="BeginSelection"/>/<see cref="AddSelectionPoint"/> are inert.
/// </summary>
public class OpaquePixelSelector(SKBitmap maskSource) : IPixelSelector
{
    private byte[]? _pixelsBuff;
    private int _left;
    private int _top;
    private int _width;
    private int _height;
    private SKPath? _selectionPath;
    private List<List<SKPoint>>? _selectionContours;

    private int MaskWidth => maskSource.Width;

    public SKPoint Offset => new(_left, _top);

    public SKPath? GetSelectionPath() => _selectionPath;

    public List<List<SKPoint>>? GetSelectionContours() => _selectionContours;

    public void BeginSelection(SKPointI point)
    {
        // No seed: the whole bitmap is the input.
    }

    public void AddSelectionPoint(SKPointI point, Action<int, int> plot)
    {
        // Not a drag gesture — nothing to accumulate.
    }

    public void FinishSelection(bool highlightSelection)
    {
        _pixelsBuff = null;
        _selectionPath = null;
        _selectionContours = null;
        _left = _top = _width = _height = 0;

        var width = maskSource.Width;
        var height = maskSource.Height;
        if (width <= 0 || height <= 0 || maskSource.BytesPerPixel != 4)
            return;

        // Rgba8888 and Bgra8888 differ only in the RGB order — alpha is the 4th byte in both, which
        // is all this selector reads, so no per-color-type branch is needed here.
        var pixelSpan = maskSource.GetPixelSpan();
        var buffer = new byte[width * height];
        var selectionPoints = new HashSet<SKPointI>();
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (pixelSpan[(x + y * width) * 4 + 3] == 0)
                    continue;

                buffer[x + y * width] = 1;
                selectionPoints.Add(new SKPointI(x, y));
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }

        if (selectionPoints.Count == 0)
            return;

        _pixelsBuff = buffer;
        _left = left;
        _top = top;
        _width = right - left + 1;
        _height = bottom - top + 1;

        if (highlightSelection)
        {
            _selectionPath = Algorithms.GetContour(
                selectionPoints,
                _pixelsBuff,
                new SKRectI(0, 0, width - 1, height - 1),
                new SKPointI(0, 0),
                new SKSizeI(width, height),
                out _selectionContours);
        }
    }

    public SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
    {
        // A 0x0 bitmap allocates but hands back a null pixel buffer far from here (see the CanvasSize
        // work), so an empty selection returns a 1x1 instead — the caller drops anything that small.
        if (_pixelsBuff == null || _width <= 0 || _height <= 0)
            return new SKBitmap(1, 1, Pix2DAppSettings.ColorType, SKAlphaType.Premul);

        var bitmap = new SKBitmap(_width, _height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        bitmap.Erase(SKColor.Empty);

        var srcWidth = sourceBitmap.Width;
        var spanSrc = sourceBitmap.GetPixelSpan();

        unsafe
        {
            var destPixelsPtr = bitmap.GetPixels(out var len);
            var spanDest = new Span<byte>(destPixelsPtr.ToPointer(), (int)len);

            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                {
                    var srcX = x + _left;
                    var srcY = y + _top;

                    if (srcX < 0 || srcY < 0 || srcX >= srcWidth || srcY >= sourceBitmap.Height)
                        continue;

                    if (!IsSelected(srcX, srcY))
                        continue;

                    var destIndex = (x + y * _width) * 4;
                    var srcIndex = (srcX + srcY * srcWidth) * 4;
                    spanDest[destIndex] = spanSrc[srcIndex];
                    spanDest[destIndex + 1] = spanSrc[srcIndex + 1];
                    spanDest[destIndex + 2] = spanSrc[srcIndex + 2];
                    spanDest[destIndex + 3] = spanSrc[srcIndex + 3];
                }
        }

        return bitmap;
    }

    public void ClearSelectionFromBitmap(ref SKBitmap bitmap)
    {
        if (_pixelsBuff == null)
            return;

        unsafe
        {
            var dest0 = (byte*)bitmap.GetPixels().ToPointer();

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    if (!IsSelected(x, y))
                        continue;

                    var dest = dest0 + (x + y * bitmap.Width) * 4;
                    *dest = 0;
                    *(dest + 1) = 0;
                    *(dest + 2) = 0;
                    *(dest + 3) = 0;
                }
        }
    }

    private bool IsSelected(int x, int y)
    {
        if (_pixelsBuff == null || x < 0 || y < 0 || x >= MaskWidth || y >= maskSource.Height)
            return false;

        return _pixelsBuff[x + y * MaskWidth] > 0;
    }
}
