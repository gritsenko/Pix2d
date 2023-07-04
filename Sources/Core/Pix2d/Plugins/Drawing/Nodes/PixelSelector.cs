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
        BuildSlectionPath();
    }

    private void SetPixel(int x, int y) => _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] = 1;
    private bool GetPixel(int x, int y)
    {
        if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
            return false;

        return _pixelsBuff[x + _offsetX + (y + _offsetY) * _width] > 0;
    }

    private void BuildSlectionPath()
    {
        bool IsPSet(int x, int y) => GetPixel(x, y);


        var sortedEdges = new List<Edge>();

        var edges = new List<Edge>();

        void AddEdge(int x0, int y0, int x1, int y1) => edges.Add(new Edge(x0, y0, x1, y1));

        {
            int x, y;
            foreach (var spt in _selectionPoints)
            {
                x = spt.X;
                y = spt.Y;

                if (!IsPSet(x, y - 1)) AddEdge(x, y, x + 1, y);
                if (!IsPSet(x, y + 1)) AddEdge(x, y + 1, x + 1, y + 1);
                if (!IsPSet(x - 1, y)) AddEdge(x, y, x, y + 1);
                if (!IsPSet(x + 1, y)) AddEdge(x + 1, y, x + 1, y + 1);

            }
        }

        for (var i = 0; i < edges.Count && edges.Any(); i++)
        {
            foreach (var edge in edges.ToArray())
            {
                if (!sortedEdges.Any() || sortedEdges.Last().IsConnectedTo(edge))
                {
                    sortedEdges.Add(edge);
                    edges.Remove(edge);
                    i = 0;
                }
                else if (sortedEdges[0].IsConnectedTo(edge))
                {
                    sortedEdges.Insert(0, edge);
                    edges.Remove(edge);
                    i = 0;
                }
            }
        }


        var corners = new List<SKPointI>();

        void AddCorner(SKPointI pt)
        {
            if (corners.Count == 0 || corners.Last() != pt)
                corners.Add(pt);
        }

        var prev = sortedEdges[0];
        var cur = sortedEdges[1];

        var firstPoint = prev.GetFreePoint(cur);
        AddCorner(firstPoint);

        for (var i = 1; i < sortedEdges.Count; i++)
        {
            prev = sortedEdges[i - 1];
            cur = sortedEdges[i];
            var pt = cur.GetConnectionPoint(prev);
            AddCorner(pt);
        }

        AddCorner(cur.GetFreePoint(prev));
        AddCorner(firstPoint);

        var path = new SKPath();
        path.AddPoly(corners.Select(p => new SKPoint(p.X, p.Y)).ToArray());

        _selectionPath = path;
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