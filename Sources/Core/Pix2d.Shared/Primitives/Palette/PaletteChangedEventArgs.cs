using System;
using System.Text;

namespace Pix2d.Primitives.Palette
{
    public class PaletteChangedEventArgs : EventArgs
    {
        public string PaletteName { get; }

        public PaletteChangedEventArgs(string paletteName)
        {
            PaletteName = paletteName;
        }
    }
}