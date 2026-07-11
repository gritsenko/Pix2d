#nullable enable
using SkiaSharp;

namespace Pix2d.Export.Sheet;

/// <summary>Finds the tight opaque bounding box of a rendered frame, for trim-packing.</summary>
public static class FrameTrimmer
{
    /// <summary>
    /// Returns the smallest rect covering every non-transparent pixel of <paramref name="bitmap"/>.
    /// A fully transparent frame returns a 1×1 rect at the origin (so it still occupies a real cell and
    /// keeps a stable placement rather than collapsing to zero size).
    /// </summary>
    public static SKRectI GetOpaqueBounds(SKBitmap bitmap)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;
        if (w <= 0 || h <= 0)
            return new SKRectI(0, 0, 1, 1);

        var bytes = bitmap.Bytes;          // managed copy of the pixel buffer (RGBA/BGRA 8888)
        var rowBytes = bitmap.RowBytes;    // may exceed w*4 if the surface is padded
        // Alpha is the 4th byte in both RGBA8888 and BGRA8888 premul layouts (unaffected by premul).
        const int bpp = 4;

        var minX = w;
        var minY = h;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < h; y++)
        {
            var row = y * rowBytes;
            for (var x = 0; x < w; x++)
            {
                if (bytes[row + x * bpp + 3] == 0)
                    continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0)
            return new SKRectI(0, 0, 1, 1); // fully transparent

        return new SKRectI(minX, minY, maxX + 1, maxY + 1);
    }
}
