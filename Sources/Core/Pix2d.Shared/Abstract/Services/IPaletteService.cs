using System;
using System.Collections.Generic;
using Pix2d.Primitives.Palette;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IPaletteService
{

    event EventHandler<PaletteChangedEventArgs> PaletteChanged;

    IReadOnlyList<SKColor> RecentPalette { get; }
    IReadOnlyList<SKColor> CustomPalette { get; }

    IEnumerable<SKColor> GetPaletteColors(string paletteName);
    void InsertColor(string paletteName, SKColor color, int index = -1);
    void RemoveColor(string paletteName, SKColor color);
}