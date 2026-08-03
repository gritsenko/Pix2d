using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Common.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace Pix2d.Plugins.Ai.Selection;

/// <param name="aiFailureHandler">
/// Called (on the pointer thread, inside the finishing gesture) when background removal could not run and
/// the selection fell back to the dragged rectangle. Lets the owning tool tell the user once instead of
/// silently handing back a rectangle from a tool named "object selection".
/// </param>
public class AiPixelSelector(Action<Exception?>? aiFailureHandler = null) : IPixelSelector
{
    private SKPointI _lastSelectionPoint;
    private readonly HashSet<SKPointI> _selectionPoints = new HashSet<SKPointI>();
    private SKPath? _selectionPath;
    private List<List<SKPoint>>? _selectionContours;
    private byte[]? _pixelsBuff;
    private int _offsetX;
    private int _offsetY;
    private int _width;
    private int _height;
    private int _imageLeft;
    private int _imageTop;
    private int _imageRight;
    private int _imageBot;
    public SKSizeI SelectionSize => new SKSizeI(_width, _height);
    public SKPath? GetSelectionPath() => _selectionPath;

    public List<List<SKPoint>>? GetSelectionContours() => _selectionContours;

    public SKPoint Offset => new SKPoint(-_offsetX, -_offsetY);

    public void AddSelectionPoint(SKPointI pos, Action<int, int> plot)
    {
        var p1 = new SKPointI(pos.X, pos.Y);
        LineDda(_lastSelectionPoint.X, _lastSelectionPoint.Y,
            p1.X, p1.Y,
            (x, y) =>
            {
                _selectionPoints.Add(new SKPointI(x, y));
                plot(x, y);
            });

        _lastSelectionPoint = p1;
    }

    public static void LineDda(int x1, int y1, int x2, int y2, Action<int, int> plot)
    {
        var dx = (float)x2 - x1;
        var dy = (float)y2 - y1;
        var adx = Math.Abs(dx);
        var ady = Math.Abs(dy);
        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
        {
            plot(x1, y1);
            return;
        }

        int maxSteps;

        maxSteps = (int)(adx >= ady ? adx : ady);

        dx = dx / maxSteps;
        dy = dy / maxSteps;


        float x = x1;
        float y = y1;

        var i = 1;
        while (i <= maxSteps)
        {
            plot((int)x, (int)y);
            x = x + dx;
            y = y + dy;
            i = i + 1;
        }
    }


    //public void DrawSelection(IDrawingSession ds)
    //{
    //    if (_selectionPath != null)
    //        ds.DrawPath(_selectionPath, GdfColor.Red, 1f);
    //}

    public void BeginSelection(SKPointI pos)
    {
        _selectionPoints.Clear();
        _selectionPath = null;
        _selectionContours = null;
        _lastSelectionPoint = pos;
    }

    public void FinishSelection(bool highlightSelection)
    {
        _selectionPath = null;
        _selectionContours = null;

        if (_selectionPoints.Count == 0)
            return;

        _imageLeft = _selectionPoints.Min(x => x.X);
        _imageTop = _selectionPoints.Min(x => x.Y);

        _imageRight = _selectionPoints.Max(x => x.X);
        _imageBot = _selectionPoints.Max(x => x.Y);

        _width = _imageRight - _imageLeft + 1;
        _height = _imageBot - _imageTop + 1;

        _offsetX = -_imageLeft;
        _offsetY = -_imageTop;
        _pixelsBuff = new byte[_width * _height];
    }

    private void SetPixel(int x, int y)
    {
        if (_pixelsBuff != null)
            _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] = 1;
    }
    
    private byte GetPixel(int x, int y)
    {
        if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
            return 0;

        if (_pixelsBuff == null)
            return 0;

        return _pixelsBuff[x + _offsetX + (y + _offsetY) * _width];
    }

    public unsafe void ClearSelectionFromBitmap(ref SKBitmap bitmap)
    {
        if (_pixelsBuff == null)
            return;

        var dest0 = (byte*)bitmap.GetPixels().ToPointer();
        var h = Math.Min(-_offsetY + _height, bitmap.Height);
        var w = Math.Min(-_offsetX + _width, bitmap.Width);
        for (var y = -_offsetY; y < h; y++)
            for (var x = -_offsetX; x < w; x++)
            {
                if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
                    continue;

                var pixelOffset = dest0 + (x + y * bitmap.Width) * 4;
                var a = (*(pixelOffset + 3) / 255f) * (1f - _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] / 255f);
                *pixelOffset = (byte)(*pixelOffset * a);
                *(pixelOffset + 1) = (byte)(*(pixelOffset + 1) * a);
                *(pixelOffset + 2) = (byte)(*(pixelOffset + 2) * a);
                *(pixelOffset + 3) = (byte)(a * 255);
            }
    }
    public unsafe SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
    {
        var bitmap = new SKBitmap(_width, _height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        _selectionPath = null;
        _selectionContours = null;

        //skip ai stuff if selection is too small
        if (_pixelsBuff == null)
            return bitmap;

        if (_width < 3 && _height < 3)
            return bitmap;

        var srcWidth = sourceBitmap.Width;

        var spanSrc = MemoryMarshal.Cast<byte, int>(sourceBitmap.GetPixelSpan());
        var destPixelsPtr = bitmap.GetPixels(out IntPtr len);
        var ptr = destPixelsPtr.ToPointer();
        var spanDest = new Span<int>(ptr, (int)len);


        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var srcX = x - _offsetX;
                var srcY = y - _offsetY;
                if (srcX >= 0 && srcY >= 0 && srcX < sourceBitmap.Width && srcY < sourceBitmap.Height)
                {
                    var srcIndex = srcY * srcWidth + srcX;
                    spanDest[y * _width + x] = spanSrc[srcIndex];
                }
            }

        var extractedMask = RemoveBackground.TryProcess(bitmap, "u2netp.onnx");
        if (extractedMask == null)
        {
            // No AI available (or inference failed): keep the whole dragged rectangle selected, exactly as
            // the plain rect tool would. Letting the exception through instead killed the app from inside
            // the pointer-released handler (appstat: DllNotFoundException, 3.11.3, Windows).
            _pixelsBuff.AsSpan().Fill(255);
            BuildSelectionPath();
            aiFailureHandler?.Invoke(RemoveBackground.LastFailure);
            return bitmap;
        }

        var maskPixels = MemoryMarshal.Cast<byte, int>(extractedMask.GetPixelSpan());

        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                _pixelsBuff[x + y * _width] = (byte)(maskPixels[x + y * _width] >> 24);

        using var canvas = bitmap.CreateCanvas();
        canvas.DrawBitmap(extractedMask, SKPoint.Empty, new SKPaint() { BlendMode = SKBlendMode.DstIn });

        bitmap = CropToMaskBounds(bitmap);
        BuildSelectionPath();

        return bitmap;
    }

    private SKBitmap CropToMaskBounds(SKBitmap bitmap)
    {
        if (_pixelsBuff == null)
            return bitmap;

        var oldWidth = _width;
        var oldHeight = _height;
        var maskLeft = oldWidth;
        var maskTop = oldHeight;
        var maskRight = -1;
        var maskBottom = -1;

        for (int y = 0; y < oldHeight; y++)
        {
            for (int x = 0; x < oldWidth; x++)
            {
                if (_pixelsBuff[x + y * oldWidth] == 0)
                    continue;

                maskLeft = Math.Min(maskLeft, x);
                maskTop = Math.Min(maskTop, y);
                maskRight = Math.Max(maskRight, x);
                maskBottom = Math.Max(maskBottom, y);
            }
        }

        if (maskRight < maskLeft || maskBottom < maskTop)
            return bitmap;

        if (maskLeft == 0 && maskTop == 0 && maskRight == oldWidth - 1 && maskBottom == oldHeight - 1)
            return bitmap;

        var newWidth = maskRight - maskLeft + 1;
        var newHeight = maskBottom - maskTop + 1;
        var croppedMask = new byte[newWidth * newHeight];

        for (int y = 0; y < newHeight; y++)
        {
            Array.Copy(
                _pixelsBuff,
                (maskTop + y) * oldWidth + maskLeft,
                croppedMask,
                y * newWidth,
                newWidth);
        }

        var croppedBitmap = new SKBitmap(newWidth, newHeight, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        var sourcePixels = MemoryMarshal.Cast<byte, int>(bitmap.GetPixelSpan());
        var croppedPixels = MemoryMarshal.Cast<byte, int>(croppedBitmap.GetPixelSpan());

        for (int y = 0; y < newHeight; y++)
        {
            var sourceRow = (maskTop + y) * oldWidth + maskLeft;
            var destRow = y * newWidth;
            for (int x = 0; x < newWidth; x++)
                croppedPixels[destRow + x] = sourcePixels[sourceRow + x];
        }

        var seedLeft = _imageLeft;
        var seedTop = _imageTop;
        _imageLeft = seedLeft + maskLeft;
        _imageTop = seedTop + maskTop;
        _imageRight = seedLeft + maskRight;
        _imageBot = seedTop + maskBottom;
        _width = newWidth;
        _height = newHeight;
        _offsetX = -_imageLeft;
        _offsetY = -_imageTop;
        _pixelsBuff = croppedMask;

        bitmap.Dispose();
        return croppedBitmap;
    }

    private void BuildSelectionPath()
    {
        if (_pixelsBuff == null)
            return;

        var selectionPoints = new HashSet<SKPointI>();
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (_pixelsBuff[x + y * _width] == 0)
                    continue;

                selectionPoints.Add(new SKPointI(x - _offsetX, y - _offsetY));
            }
        }

        if (selectionPoints.Count == 0)
            return;

        _selectionPath = Algorithms.GetContour(
            selectionPoints,
            _pixelsBuff,
            new SKRectI(_imageLeft, _imageTop, _imageRight, _imageBot),
            new SKPointI(_offsetX, _offsetY),
            new SKSizeI(_width, _height),
            out var contours);
        _selectionContours = contours;
    }

}