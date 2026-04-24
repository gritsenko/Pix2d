using SkiaSharp;

namespace Pix2d.UI.Shared
{
    public class ColorChangedEventArgs (SKColor oldColor, SKColor newColor) : EventArgs
    {
        public SKColor OldColor { get; } = oldColor;
        public SKColor NewColor { get; } = newColor;
    }
}