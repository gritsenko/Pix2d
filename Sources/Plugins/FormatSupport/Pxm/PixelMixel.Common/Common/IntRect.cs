using System;

namespace PixelMixel.Common
{
    public class IntRect
    {
        public IntPoint Point0 { get; set; }
        public IntPoint Point1 { get; set; }

        public int Width => Math.Abs(Point1.X - Point0.X);
        public int Height => Math.Abs(Point1.Y - Point0.Y);

        public IntSize Size => new IntSize(Width, Height);
        public int Left => Point0.X;
        public int Top => Point0.Y;
        public int Right => Point1.X;
        public int Bottom => Point1.Y;

        public IntRect(IntPoint point0, IntSize size)
        {
            Point0 = point0;
            Point1 = point0 + size;
        }

        public IntRect(IntPoint point0, IntPoint point1)
        {
            Point0 = point0;
            Point1 = point1;
            Normalize();
        }

        public IntRect()
        {
            Point0 = new IntPoint();
            Point1 = new IntPoint();
        }

        public IntRect(IntSize size) : this(default(IntPoint), size)
        {
        }

        //public Rect GetRect(int scale = 1)
        //{
        //    return new Rect(Point0.X*scale, Point0.Y*scale, Point1.X*scale, Point1.Y*scale);
        //}

        public void SwapHirzontal()
        {
            var p0 = Point0;
            var p1 = Point1;

            Point0 = new IntPoint(p1.X, p0.Y);
            Point1 = new IntPoint(p0.X, p1.Y);
        }

        public void SwapVertical()
        {
            var p0 = Point0;
            var p1 = Point1;

            Point0 = new IntPoint(p0.X, p1.Y);
            Point1 = new IntPoint(p1.X, p0.Y);
        }


        public static IntRect operator +(IntRect rect, IntPoint point)
        {
            var p0 = rect.Point0 + point;
            var p1 = rect.Point1 + point;
            return new IntRect(p0, p1);
        }

        //public static implicit operator Rect(IntRect p)
        //{
        //    return new Rect(p.Point0, p.Point1);
        //}

        public void Normalize()
        {
            if (Point0.X > Point1.X)
            {
                SwapHirzontal();
            }

            if (Point0.Y > Point1.Y)
            {
                SwapVertical();
            }
        }


        public bool IsPointInside(IntPoint poisition)
        {
            return poisition.X >= Point0.X && poisition.Y >= Point0.Y
                   && poisition.X <= Point1.X && poisition.Y <= Point1.Y;
        }
    }
}