using System.Drawing;

namespace PixelMixel.Common
{
    public class PixColor
    {
        public PixColor()
        {
            
        }
        public PixColor(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public Color ToColor()
        {
            return Color.FromArgb(A, R, G, B);
        }
    }
}