using System;

namespace Pix2d.Infrastructure.GA.Extensions
{
    public static class TimeSpanExtensions
    {
        public const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        public static double TotalMicroseconds(this TimeSpan self) => self.Ticks * (1d / TicksPerMicrosecond);
    }
}
