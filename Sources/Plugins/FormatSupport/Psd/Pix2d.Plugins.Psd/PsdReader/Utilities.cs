namespace Pix2d.Plugins.Psd.PsdReader
{
    public class Utilities
    {
        public static ushort SwapBytes(ushort x)
            => (ushort) ((ushort) ((x & 255) << 8) | (x >> 8 & 255));

        public static uint SwapBytes(uint x)
        {
            x = (x >> 16 | x << 16);
            return (x & 4278255360u) >> 8 | (x & 16711935u) << 8;
        }

        public static ulong SwapBytes(ulong x)
        {
            x = (x >> 32 | x << 32);
            x = ((x & 18446462603027742720UL) >> 16 | (x & 281470681808895UL) << 16);
            return (x & 18374966859414961920UL) >> 8 | (x & 71777214294589695UL) << 8;
        }

        public static short SwapBytes(short x) => (short) SwapBytes((ushort) x);

        public static int SwapBytes(int x) => (int) SwapBytes((uint) x);

        public static long SwapBytes(long x) => (long) SwapBytes((ulong) x);
    }
}