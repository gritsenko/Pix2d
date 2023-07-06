using System;
using System.Collections.Generic;
using Pix2d.Abstract.Drawing;
using Pix2d.Common.Drawing;
using SkiaSharp;

namespace Pix2d.Drawing.Nodes
{
    public class SameColorSelector : IPixelSelector
    {
        private readonly SKBitmap _bitmap;
        private SKPointI _pixelPos;
        private SKColor _color;
        private byte[] _pixelsBuff;
        private int _offsetX;
        private int _offsetY;
        private int _width;
        private int _height;
        private int _imageLeft;
        private int _imageTop;
        private int _imageRight;
        private int _imageBot;
        private SKPath _selectionPath;

        public SKPath GetSelectionPath()
        {
            return _selectionPath;
        }

        public SKPoint Offset => new SKPoint(_offsetX, _offsetY);

        public SameColorSelector(SKBitmap bitmap)
        {
            _bitmap = bitmap;
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
            _color = SKColor.Empty;

            if (_pixelPos.X < 0)
                return;

            if (_pixelPos.Y < 0)
                return;

            if (_pixelPos.X > _bitmap.Width)
                return;

            if (_pixelPos.Y > _bitmap.Height)
                return;

            _pixelsBuff = new byte[_bitmap.Width * _bitmap.Height];

            _color = _bitmap.GetPixel((int)_pixelPos.X, (int)_pixelPos.Y);

            var sourceBitmap = _bitmap;

            var srcWidth = sourceBitmap.Width;

            var left = sourceBitmap.Width;
            var top = sourceBitmap.Height;
            var right = 0;
            var bottom = 0;

            var spanSrc = sourceBitmap.GetPixelSpan();
            var selectionPoints = new HashSet<SKPointI>();

            for (int y = 0; y < sourceBitmap.Height; y++)
            for (int x = 0; x < sourceBitmap.Width; x++)
            {
                var srcX = x;
                var srcY = y;
                var srcIndex = (srcX + srcY * srcWidth) * 4;

                if (srcX >= 0 && srcY >= 0 && srcX < sourceBitmap.Width && srcY < sourceBitmap.Height)
                    if (spanSrc[srcIndex] == _color.Blue
                        && spanSrc[srcIndex + 1] == _color.Green
                        && spanSrc[srcIndex + 2] == _color.Red
                        && spanSrc[srcIndex + 3] == _color.Alpha)
                    {
                        left = Math.Min(left, x);
                        top = Math.Min(top, y);

                        right = Math.Max(right, x);
                        bottom = Math.Max(bottom, y);
                        _pixelsBuff[x + y * sourceBitmap.Width] = 1;
                        selectionPoints.Add(new SKPointI(srcX, srcY));
                    }
            }

            _offsetX = left;
            _offsetY = top;

            _imageLeft = 0;
            _imageTop = 0;

            _imageRight = _bitmap.Width;
            _imageBot = _bitmap.Height;

            _width = right - left + 1;
            _height = bottom - top + 1;

            if (highlightSelection)
            {
                _selectionPath = Algorithms.GetContour(selectionPoints, _pixelsBuff,
                    new SKRectI(0, 0, _bitmap.Width - 1, _bitmap.Height - 1), new SKPointI(0, 0),
                    new SKSizeI(_bitmap.Width, _bitmap.Height));
            }
            else
            {
                _selectionPath = null;
            }
        }

        private bool GetPixel(int x, int y)
        {
            if (x < _imageLeft || y < _imageTop || x > _imageRight || y > _imageBot)
                return false;

            return _pixelsBuff[x + (y ) * _bitmap.Width] > 0;
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

                for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    var srcX = x + _offsetX;
                    var srcY = y + _offsetY;

                    if (srcX >= 0 && srcY >= 0 && srcX < sourceBitmap.Width && srcY < sourceBitmap.Height)
                        if (_pixelsBuff[srcX + srcY * sourceBitmap.Width] > 0)
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

        //public void ClearSelectionFromBitmap(ref SKBitmap bitmap)
        //{
        //    for (int y = 0; y < bitmap.Height; y++)
        //        for (int x = 0; x < bitmap.Width; x++)
        //        {
        //            if (bitmap.GetPixel(x, y) == _color)
        //                bitmap.SetPixel(x, y, SKColor.Empty);
        //        }
        //}

        public void ClearSelectionFromBitmap(ref SKBitmap bitmap)
        {
            if (_pixelsBuff == null || bitmap == null)
                return;
            
            unsafe
            {
                //fixed (byte* pSource = data)
                {
                    var dest0 = (byte*)bitmap.GetPixels().ToPointer();
                    //Buffer.MemoryCopy(pSource, bitmap.GetPixels().ToPointer(), data.Length, data.Length);

                    for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (GetPixel(x, y))
                            //bitmap.SetPixel(x, y, SKColor.Empty);
                        {
                            var dest = dest0 + (x + y * bitmap.Width) * 4;
                            *dest = 0;
                            *(dest + 1) = 0;
                            *(dest + 2) = 0;
                            *(dest + 3) = 0;
                        }
                    }
                }
            }
        }


    }
}