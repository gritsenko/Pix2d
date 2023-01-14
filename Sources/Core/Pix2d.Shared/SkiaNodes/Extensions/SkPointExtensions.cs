using System;
using SkiaSharp;

namespace SkiaNodes.Extensions
{
    public static class SKPointExtensions
    {
        public static SKPoint Multiply(this SKPoint a, SKPoint b) => new SKPoint(a.X * b.X, a.Y * b.Y);
        public static SKPoint Multiply(this SKPoint a, float b) => new SKPoint(a.X * b, a.Y * b);

        public static float GetVectorLength(this SKPoint p) => (float) Math.Sqrt(p.X * p.X + p.Y * p.Y);
        public static SKPoint GetLeftTopPoint(this SKRect rect) => new SKPoint(rect.Left, rect.Top);
        public static SKPoint GetRightTopPoint(this SKRect rect) => new SKPoint(rect.Right, rect.Top);
        public static SKPoint GetLeftBottomPoint(this SKRect rect) => new SKPoint(rect.Left, rect.Bottom);

        public static SKPoint GetRightBottomPoint(this SKRect rect) => new SKPoint(rect.Right, rect.Bottom);

        public static SKRect ToSKRect(SKPoint p0, SKPoint p1)
            => new SKRect(p0.X, p0.Y, p1.X, p1.Y);

        public static float SnapToGrid(this float x, float cellx) => (float)(Math.Floor(x / cellx) * cellx);

        public static SKPoint SnapToGrid(this SKPoint v, float cellSize)
        {
            return new SKPoint(SnapToGrid(v.X, cellSize), SnapToGrid(v.Y, cellSize));
        }
        public static SKPoint SnapToGrid(this SKPoint v, SKSize cellSize)
        {
            return new SKPoint(SnapToGrid(v.X, cellSize.Width), SnapToGrid(v.Y, cellSize.Height));
        }

        public static SKPoint Floor(this SKPoint p)
        {
            return new SKPoint((float)Math.Floor(p.X), (float)Math.Floor(p.Y));
        }

        public static SKPointI ToSkPointI(this SKPoint p) => new SKPointI((int) p.X, (int) p.Y);
    }
}