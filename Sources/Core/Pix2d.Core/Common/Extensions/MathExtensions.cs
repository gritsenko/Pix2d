using System;
using System.Collections.Generic;
using System.Text;

namespace Pix2d.Common.Extensions
{
    public static class MathExtensions
    {
        public static float Clamp(this float checkValue, float min, float max)
        {
            if (checkValue < min) return min;
            return checkValue > max ? max : checkValue;
        }
    }
}
