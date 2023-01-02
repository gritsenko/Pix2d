using System;
using SkiaSharp;

namespace SkiaNodes.Extensions
{
    public static class SkPointIExtensions
    {
        public static float GetVectorLength(this SKPointI p) => (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);
        public static float DistanceTo(this SKPointI p, SKPointI other)
        {
            var dp = other - p;
            return (float) Math.Sqrt(dp.X * dp.X + dp.Y * dp.Y);
        }
    }
}