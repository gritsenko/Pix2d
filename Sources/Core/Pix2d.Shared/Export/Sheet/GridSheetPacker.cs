#nullable enable
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Pix2d.Export.Sheet;

/// <summary>
/// Uniform-grid packer: one cell per frame, <see cref="SpriteSheetOptions.MaxColumns"/> columns wide.
/// Cells are sized to the largest frame; each frame sits at its cell's top-left (its trim offset is
/// carried in the metadata, so a smaller trimmed frame in a bigger cell still maps back correctly).
/// </summary>
public sealed class GridSheetPacker : ISheetPacker
{
    public PackResult Pack(IReadOnlyList<SKSizeI> frameSizes, SpriteSheetOptions options)
    {
        var count = frameSizes.Count;
        if (count == 0)
            return new PackResult(Array.Empty<SKPointI>(), new SKSizeI(1, 1));

        var padding = Math.Max(0, options.Padding);
        var cols = Math.Min(Math.Max(1, options.MaxColumns), count);
        var rows = (int)Math.Ceiling(count / (double)cols);

        var cellW = 0;
        var cellH = 0;
        foreach (var s in frameSizes)
        {
            if (s.Width > cellW) cellW = s.Width;
            if (s.Height > cellH) cellH = s.Height;
        }

        var positions = new SKPointI[count];
        for (var i = 0; i < count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            positions[i] = new SKPointI(col * (cellW + padding), row * (cellH + padding));
        }

        var width = cols * cellW + (cols - 1) * padding;
        var height = rows * cellH + (rows - 1) * padding;
        return new PackResult(positions, new SKSizeI(width, height));
    }
}
