using System;

namespace PixelMixel.Common
{
    public struct IntPoint
    {
        public IntPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }

        public static IntPoint operator +(IntPoint point, IntSize size)
        {
            return new IntPoint(point.X + size.Width, point.Y + size.Height);
        }

        public static IntPoint operator +(IntPoint p0, IntPoint p1)
        {
            return new IntPoint(p0.X + p1.X, p0.Y + p1.Y);
        }

        public static IntPoint operator *(IntPoint p0, int m)
        {
            return new IntPoint(p0.X*m, p0.Y*m);
        }

        public static IntPoint operator -(IntPoint p0, IntPoint p1)
        {
            return new IntPoint(p0.X - p1.X, p0.Y - p1.Y);
        }


        //public static implicit operator Point(IntPoint p)
        //{
        //    return new Point(p.X, p.Y);
        //}


        public bool Equals(IntPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is IntPoint && Equals((IntPoint) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X*397) ^ Y;
            }
        }

        public static bool operator ==(IntPoint left, IntPoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(IntPoint left, IntPoint right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{X}:{Y}";
        }

        public double DistanceTo(IntPoint point)
        {
            return Math.Sqrt((X - point.X)*(X - point.X) + (Y - point.Y)*(Y - point.Y));
        }
    }
}