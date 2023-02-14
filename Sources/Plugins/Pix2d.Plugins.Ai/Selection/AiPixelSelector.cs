using Pix2d.Abstract.Drawing;
using Pix2d.Common.Drawing;
using SkiaSharp;
using System.Runtime.InteropServices;
using System;
using SkiaNodes.Extensions;
using static Pix2d.CommonNodes.Pix2dSprite;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Pix2d.Plugins.Ai.Selection;

public class AiPixelSelector : IPixelSelector
{
    private SKPointI _lastSelectionPoint;
    private readonly HashSet<SKPointI> _selectionPoints = new HashSet<SKPointI>();
    //private GdfPath _selectionPath;
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
        //BuildSlectionPath();
    }

    private void SetPixel(int x, int y) => _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] = 1;
    private byte GetPixel(int x, int y)
    {
        if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
            return 0;

        return _pixelsBuff[x + _offsetX + (y + _offsetY) * _width];
    }

    public unsafe void ClearSelectionFromBitmap(ref SKBitmap bitmap)
    {
        var dest0 = (byte*)bitmap.GetPixels().ToPointer();
        var h = bitmap.Height;
        var w = bitmap.Width;
        for (int y = -_offsetY; y < -_offsetY + _height; y++)
            for (int x = -_offsetX; x < -_offsetX + _width; x++)
            {
                var a = GetPixel(x, y);
                var dest = dest0 + (x + y * bitmap.Width) * 4;
                if (a > 0)
                {
                    //*dest = 0;
                    //*(dest + 1) = 0;
                    //*(dest + 2) = 0;
                    *(dest + 3) = a;
                }
            }
    }
    public unsafe SKBitmap GetSelectionBitmap(SKBitmap sourceBitmap)
    {
        var bitmap = new SKBitmap(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);

        //skip ai stuff if selection is too small
        if (_width < 3 && _height == 3)
            return bitmap;

        var srcWidth = sourceBitmap.Width;

        var spanSrc = MemoryMarshal.Cast<byte, int>(sourceBitmap.GetPixelSpan());
        var destPixelsPtr = bitmap.GetPixels(out IntPtr len);
        var ptr = destPixelsPtr.ToPointer();
        var spanDest = new Span<int>(ptr, (int)len);

        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                spanDest[y * _width + x] = spanSrc[(y - _offsetY) * srcWidth + (x - _offsetX)];

        var extractedMask = RemoveBackground.Process(bitmap, "u2netp.onnx");

        using var canvas = bitmap.GetSKSurface().Canvas;
        canvas.DrawBitmap(extractedMask, SKPoint.Empty, new SKPaint() { BlendMode = SKBlendMode.DstIn });
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                _pixelsBuff[x + y * _width] = (byte)(spanDest[y * _width + x] & 0xff);

        return bitmap;
    }

}