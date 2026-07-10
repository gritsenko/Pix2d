using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Primitives.Palette;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IPaletteService
{

    event EventHandler<PaletteChangedEventArgs> PaletteChanged;

    /// <summary>Raised when the named-palette library changes (save / delete / import).</summary>
    event EventHandler SavedPalettesChanged;

    IReadOnlyList<SKColor> RecentPalette { get; }
    IReadOnlyList<SKColor> CustomPalette { get; }

    IEnumerable<SKColor> GetPaletteColors(string paletteName);
    void InsertColor(string paletteName, SKColor color, int index = -1);
    void RemoveColor(string paletteName, SKColor color);

    /// <summary>Replaces every color in the given palette (used when loading / importing a palette).</summary>
    void SetPaletteColors(string paletteName, IEnumerable<SKColor> colors);

    // ---- Named palette library ---------------------------------------------

    IReadOnlyList<string> GetSavedPaletteNames();

    /// <summary>Snapshots the current custom palette into the library under <paramref name="name"/> (overwrites a same-named entry).</summary>
    void SaveCurrentPaletteAs(string name);

    /// <summary>Replaces the custom palette with the saved palette's colors.</summary>
    void LoadSavedPalette(string name);

    void DeleteSavedPalette(string name);

    // ---- File / remote import-export ---------------------------------------

    /// <summary>Shows an open dialog and loads a palette from a <c>.gpl</c> / <c>.hex</c> / <c>.txt</c> / <c>.png</c> file. Returns false on cancel/failure.</summary>
    Task<bool> ImportPaletteFromFileAsync();

    /// <summary>Shows a save dialog and writes the current custom palette; the chosen extension picks the format.</summary>
    Task<bool> ExportPaletteToFileAsync(string suggestedName);

    /// <summary>Fetches a palette from lospec.com (accepts a slug or a full palette URL). Returns false on failure.</summary>
    Task<bool> ImportPaletteFromLospecAsync(string slugOrUrl);
}
