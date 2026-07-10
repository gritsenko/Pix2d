using System.Collections.Generic;

namespace Pix2d.Primitives.Palette;

/// <summary>
/// A named custom palette stored in the user's palette library (persisted via
/// <c>AppSettings.SavedPalettes</c>). Colors are kept as hex strings
/// (<c>#AARRGGBB</c> / <c>#RRGGBB</c>, as produced by <see cref="SkiaSharp.SKColor.ToString"/>)
/// so the model round-trips cleanly through the reflection-based settings serializer.
/// </summary>
public class PaletteData
{
    public string Name { get; set; } = "";
    public List<string> Colors { get; set; } = new();
}
