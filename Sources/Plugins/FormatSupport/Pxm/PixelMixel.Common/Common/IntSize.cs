namespace PixelMixel.Common
{
    public struct IntSize
    {
        public IntSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public int Space => Width*Height;

        //public static implicit operator IntSize(Size size)
        //{
        //    return new IntSize((int)size.Width, (int)size.Height);
        //}

        //public static implicit operator Size(IntSize size)
        //{
        //    return new Size(size.Width, size.Height);
        //}

        public static IntSize operator *(IntSize size, int m)
        {
            return new IntSize(size.Width * m, size.Height * m);
        }


        public IntSize Swap()
        {
            return new IntSize(Height, Width);
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }
}