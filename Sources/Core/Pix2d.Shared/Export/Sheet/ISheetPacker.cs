#nullable enable
using System.Collections.Generic;
using SkiaSharp;

namespace Pix2d.Export.Sheet;

/// <summary>Result of a packing pass: a placement position per input frame + the tight sheet size.</summary>
public sealed record PackResult(IReadOnlyList<SKPointI> Positions, SKSizeI Size);

/// <summary>
/// Lays out a set of frame sizes into a single sheet. Pure geometry — packers never touch bitmaps,
/// so they are trivially unit-testable and shared between the app exporter and the CLI.
/// </summary>
public interface ISheetPacker
{
    /// <param name="frameSizes">Size of each frame (trimmed or full), in the source order.</param>
    /// <param name="options">Packing options (columns, padding).</param>
    /// <returns>One top-left position per frame (same order/length) and the used sheet size.</returns>
    PackResult Pack(IReadOnlyList<SKSizeI> frameSizes, SpriteSheetOptions options);
}
