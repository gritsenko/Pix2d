using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Common.Drawing;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.PixelSelectors;

public class SameColorSelector : IPixelSelector
{
    private readonly SKBitmap _bitmap;
    private readonly int _tolerance;
    private readonly ColorSelectionScope _scope;
    private SKPointI _pixelPos;
    private SKColor _color;
    private byte[]? _pixelsBuff;
    private int _offsetX;
    private int _offsetY;
    private int _width;
    private int _height;
    private int _imageLeft;
    private int _imageTop;
    private int _imageRight;
    private int _imageBot;
    private SKPath? _selectionPath;
    private List<List<SKPoint>>? _selectionContours;

    public SKPath? GetSelectionPath() => _selectionPath;

    public List<List<SKPoint>>? GetSelectionContours() => _selectionContours;

    public SKPoint Offset => new SKPoint(_offsetX, _offsetY);

    public SameColorSelector(SKBitmap bitmap, int tolerance, ColorSelectionScope scope)
    {
        _bitmap = bitmap;
        _tolerance = Math.Clamp(tolerance, 0, 255);
        _scope = scope;
    }

    public void BeginSelection(SKPointI point)
    {
        _pixelPos = point;
    }

    public void AddSelectionPoint(SKPointI point, Action<int, int> plot)
    {
    }

    public void FinishSelection(bool highlightSelection)
    {
        ResetSelectionState();

        if (_pixelPos.X < 0 || _pixelPos.Y < 0 || _pixelPos.X >= _bitmap.Width || _pixelPos.Y >= _bitmap.Height)
            return;

        _pixelsBuff = new byte[_bitmap.Width * _bitmap.Height];
        _color = _bitmap.GetPixel(_pixelPos.X, _pixelPos.Y);

        var left = _bitmap.Width;
        var top = _bitmap.Height;
        var right = -1;
        var bottom = -1;
        var selectionPoints = new HashSet<SKPointI>();
        var pixelSpan = _bitmap.GetPixelSpan();

        if (_scope == ColorSelectionScope.WholeLayer)
            SelectWholeLayer(pixelSpan, selectionPoints, ref left, ref top, ref right, ref bottom);
        else
            FloodFill(pixelSpan, selectionPoints, ref left, ref top, ref right, ref bottom);

        if (selectionPoints.Count == 0)
            return;

        _offsetX = left;
        _offsetY = top;
        _imageLeft = 0;
        _imageTop = 0;
        _imageRight = _bitmap.Width - 1;
        _imageBot = _bitmap.Height - 1;
        _width = right - left + 1;
        _height = bottom - top + 1;

        if (highlightSelection)
        {
            _selectionPath = Algorithms.GetContour(
                selectionPoints,
                _pixelsBuff,
                new SKRectI(0, 0, _bitmap.Width - 1, _bitmap.Height - 1),
                new SKPointI(0, 0),
                new SKSizeI(_bitmap.Width, _bitmap.Height),
                out _selectionContours);
        }
    }

    private void ResetSelectionState()
    {
        _color = SKColor.Empty;
        _pixelsBuff = null;
        _selectionPath = null;
        _selectionContours = null;
        _offsetX = 0;
        _offsetY = 0;
        _width = 0;
        _height = 0;
        _imageLeft = 0;
        _imageTop = 0;
        _imageRight = 0;
        _imageBot = 0;
    }

    private void SelectWholeLayer(Span<byte> pixelSpan, HashSet<SKPointI> selectionPoints, ref int left, ref int top, ref int right, ref int bottom)
    {
        for (int y = 0; y < _bitmap.Height; y++)
        for (int x = 0; x < _bitmap.Width; x++)
        {
            if (!MatchesSeedColor(pixelSpan, x, y))
                continue;

            SelectPixel(x, y, selectionPoints, ref left, ref top, ref right, ref bottom);
        }
    }

    private void FloodFill(Span<byte> pixelSpan, HashSet<SKPointI> selectionPoints, ref int left, ref int top, ref int right, ref int bottom)
    {
        var width = _bitmap.Width;
        var height = _bitmap.Height;
        var visited = new bool[width * height];
        var queue = new Queue<SKPointI>();
        queue.Enqueue(_pixelPos);

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (point.X < 0 || point.Y < 0 || point.X >= width || point.Y >= height)
                continue;

            var index = point.X + point.Y * width;
            if (visited[index])
                continue;

            visited[index] = true;

            if (!MatchesSeedColor(pixelSpan, point.X, point.Y))
                continue;

            SelectPixel(point.X, point.Y, selectionPoints, ref left, ref top, ref right, ref bottom);

            queue.Enqueue(new SKPointI(point.X - 1, point.Y));
            queue.Enqueue(new SKPointI(point.X + 1, point.Y));
            queue.Enqueue(new SKPointI(point.X, point.Y - 1));
            queue.Enqueue(new SKPointI(point.X, point.Y + 1));
        }
    }

    private void SelectPixel(int x, int y, HashSet<SKPointI> selectionPoints, ref int left, ref int top, ref int right, ref int bottom)
    {
        left = Math.Min(left, x);
        top = Math.Min(top, y);
        right = Math.Max(right, x);
        bottom = Math.Max(bottom, y);
        _pixelsBuff![x + y * _bitmap.Width] = 1;
        selectionPoints.Add(new SKPointI(x, y));
    }

    private bool MatchesSeedColor(Span<byte> pixelSpan, int x, int y)
    {
        var index = (x + y * _bitmap.Width) * 4;

        return _bitmap.ColorType switch
        {
            SKColorType.Bgra8888 => ChannelWithinTolerance(pixelSpan[index + 2], _color.Red)
                && ChannelWithinTolerance(pixelSpan[index + 1], _color.Green)
                && ChannelWithinTolerance(pixelSpan[index], _color.Blue)
                && ChannelWithinTolerance(pixelSpan[index + 3], _color.Alpha),
            SKColorType.Rgba8888 => ChannelWithinTolerance(pixelSpan[index], _color.Red)
                && ChannelWithinTolerance(pixelSpan[index + 1], _color.Green)
                && ChannelWithinTolerance(pixelSpan[index + 2], _color.Blue)
                && ChannelWithinTolerance(pixelSpan[index + 3], _color.Alpha),
            _ => throw new Exception("Sorry, I don't support this color type")
        };
    }

    private bool ChannelWithinTolerance(byte value, byte seedValue)
    {
        return Math.Abs(value - seedValue) <= _tolerance;
    }

    private bool GetPixel(int x, int y)
    {
        if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
            return false;

        return _pixelsBuff != null && _pixelsBuff[x + y * _bitmap.Width] > 0;
    }

    public SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
    {
        var bitmap = new SKBitmap(_width, _height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        bitmap.Erase(SKColor.Empty);

        var srcWidth = sourceBitmap.Width;

        unsafe
        {
            var spanSrc = sourceBitmap.GetPixelSpan();
            var destPixelsPtr = bitmap.GetPixels(out IntPtr len);
            var ptr = destPixelsPtr.ToPointer();
            var spanDest = new Span<byte>(ptr, (int)len);

            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                var srcX = x + _offsetX;
                var srcY = y + _offsetY;

                if (srcX >= 0 && srcY >= 0 && srcX < sourceBitmap.Width && srcY < sourceBitmap.Height
                    && _pixelsBuff![srcX + srcY * sourceBitmap.Width] > 0)
                {
                    var destIndex = (x + y * _width) * 4;
                    var srcIndex = (srcX + srcY * srcWidth) * 4;
                    spanDest[destIndex] = spanSrc[srcIndex];
                    spanDest[destIndex + 1] = spanSrc[srcIndex + 1];
                    spanDest[destIndex + 2] = spanSrc[srcIndex + 2];
                    spanDest[destIndex + 3] = spanSrc[srcIndex + 3];
                }
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

            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (!GetPixel(x, y))
                    continue;

                var dest = dest0 + (x + y * bitmap.Width) * 4;
                *dest = 0;
                *(dest + 1) = 0;
                *(dest + 2) = 0;
                *(dest + 3) = 0;
            }
        }
    }
}
