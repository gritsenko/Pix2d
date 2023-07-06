using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Drawing;
using Pix2d.Common.Drawing;
using SkiaSharp;

namespace Pix2d.Drawing.Nodes;

public class PixelSelector : IPixelSelector
{
    private SKPointI _lastSelectionPoint;
    private readonly HashSet<SKPointI> _selectionPoints = new HashSet<SKPointI>();
    private SKPath _selectionPath;
    private byte[] _pixelsBuff;
    private int _offsetX;
    private int _offsetY;
    private int _width;
    private int _height;
    private int _imageLeft;
    private int _imageTop;
    private int _imageRight;
    private int _imageBot;
    public SKSizeI SelectionSize => new SKSizeI(_width, _height);
    public SKPoint Offset => new SKPoint(-_offsetX, -_offsetY);

    public void AddSelectionPoint(SKPointI pos, Action<int, int> plot)
    {
        var p1 = new SKPointI(pos.X, pos.Y);
        Algorithms.LineDda(_lastSelectionPoint.X, _lastSelectionPoint.Y,
            p1.X, p1.Y,
            (x, y) =>
            {
                _selectionPoints.Add(new SKPointI(x, y));
                plot(x, y);
            });

        _lastSelectionPoint = p1;
    }

    //public void DrawSelection(IDrawingSession ds)
    //{
    //    if (_selectionPath != null)
    //        ds.DrawPath(_selectionPath, GdfColor.Red, 1f);
    //}

    public void BeginSelection(SKPointI pos)
    {
        _selectionPoints.Clear();
        _lastSelectionPoint = pos;
    }

    public void FinishSelection()
    {
        var pts = _selectionPoints.Select(x => new SKPoint(x.X, x.Y)).ToArray();

        _imageLeft = _selectionPoints.Min(x => x.X);
        _imageTop = _selectionPoints.Min(x => x.Y);

        _imageRight = _selectionPoints.Max(x => x.X);
        _imageBot = _selectionPoints.Max(x => x.Y);

        _width = _imageRight - _imageLeft + 1;
        _height = _imageBot - _imageTop + 1;

        _offsetX = -_imageLeft;
        _offsetY = -_imageTop;
        _pixelsBuff = new byte[_width * _height];

        foreach (var p in _selectionPoints)
            SetPixel(p.X, p.Y);

        Algorithms.FillPolygon(pts, SetPixel);
        BuildSelectionPath();
    }

    private void SetPixel(int x, int y) => _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] = 1;
    private bool GetPixel(int x, int y)
    {
        if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
            return false;

        return _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] > 0;
    }

    private void BuildSelectionPath()
    {
        _selectionPath = Algorithms.GetContour(_selectionPoints, _pixelsBuff, new SKRectI(_imageLeft, _imageTop, _imageRight, _imageBot), new SKPointI(_offsetX, _offsetY), new SKSizeI(_width, _height));
    }

    public void ClearSelectionFromBitmap(ref SKBitmap bitmap)
    {

        unsafe
        {
            //fixed (byte* pSource = data)
            {
                var dest0 = (byte*) bitmap.GetPixels().ToPointer();
                var h = bitmap.Height;
                var w = bitmap.Width;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (GetPixel(x, y))
                    {
                        var dest = dest0 + (x + y * bitmap.Width) * 4;
                        *dest = 0;
                        *(dest+1) = 0;
                        *(dest+2) = 0;
                        *(dest+3) = 0;
                    }
                }
            }
        }
    }

    public void ClearInvertedSelectionFromBitmap(SKBitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (!GetPixel(x, y))
                bitmap.SetPixel(x, y, SKColor.Empty);
        }
    }

    public SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
    {
        var bitmap = new SKBitmap(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(SKColor.Empty);

        var srcWidth = sourceBitmap.Width;

        unsafe
        {
            var spanSrc = sourceBitmap.GetPixelSpan();
            var destPixelsPtr = bitmap.GetPixels(out IntPtr len);
            var ptr = destPixelsPtr.ToPointer();
            var spanDest = new Span<byte>(ptr, (int)len);

            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {

                if (_pixelsBuff[x + y * _width] > 0)
                {
                    var srcX = x - _offsetX;
                    var srcY = y - _offsetY;

                    if (srcX >= 0 && srcY >= 0 && srcX < sourceBitmap.Width && srcY < sourceBitmap.Height)
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
        }

        return bitmap;
    }
    
    public SKPath GetSelectionPath()
    {
        return _selectionPath;
    }
}