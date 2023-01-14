using System;
using SkiaSharp;

namespace SkiaNodes.Extensions
{
    public static class SKSizeExtensions
    {
        public static SKSize Floor(this SKSize p)
        {
            return new SKSize((float)Math.Floor(p.Width), (float)Math.Floor(p.Height));
        }
        public static SKSize Ceiling(this SKSize p)
        {
            return new SKSize((float)Math.Ceiling(p.Width), (float)Math.Ceiling(p.Height));
        }

        public static float GetAspect(this SKSize size)
        {
            return size.Width / size.Height;
        }
        public static float GetSpace(this SKSize size)
        {
            return size.Width * size.Height;
        }
    }
}