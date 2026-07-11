#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Pix2d.Export.Sheet;

/// <summary>
/// Shelf (row) packer for tight sheets. Frames are placed left-to-right on rows ("shelves"); a frame
/// that would overflow the target width starts a new shelf below the tallest frame so far. Frames are
/// visited tallest-first so shelves stay compact. For same-size frames this yields a near-square grid;
/// for trimmed frames of differing heights it packs noticeably tighter than a uniform grid.
/// </summary>
public sealed class ShelfSheetPacker : ISheetPacker
{
    public PackResult Pack(IReadOnlyList<SKSizeI> frameSizes, SpriteSheetOptions options)
    {
        var count = frameSizes.Count;
        if (count == 0)
            return new PackResult(Array.Empty<SKPointI>(), new SKSizeI(1, 1));

        var padding = Math.Max(0, options.Padding);

        var maxW = 0;
        long totalArea = 0;
        foreach (var s in frameSizes)
        {
            if (s.Width > maxW) maxW = s.Width;
            totalArea += (long)(s.Width + padding) * (s.Height + padding);
        }

        // Aim for a roughly square sheet, but never narrower than the widest single frame.
        var targetW = Math.Max(maxW, (int)Math.Ceiling(Math.Sqrt(totalArea)));

        // Visit tallest-first for compact shelves, but keep the mapping back to source order.
        var order = Enumerable.Range(0, count)
            .OrderByDescending(i => frameSizes[i].Height)
            .ThenByDescending(i => frameSizes[i].Width)
            .ToArray();

        var positions = new SKPointI[count];
        int x = 0, y = 0, shelfH = 0, usedW = 0;

        foreach (var i in order)
        {
            var (w, h) = (frameSizes[i].Width, frameSizes[i].Height);

            if (x > 0 && x + w > targetW)
            {
                // start a new shelf
                x = 0;
                y += shelfH + padding;
                shelfH = 0;
            }

            positions[i] = new SKPointI(x, y);
            x += w + padding;
            if (x - padding > usedW) usedW = x - padding;
            if (h > shelfH) shelfH = h;
        }

        var usedH = y + shelfH;
        return new PackResult(positions, new SKSizeI(usedW, usedH));
    }
}
