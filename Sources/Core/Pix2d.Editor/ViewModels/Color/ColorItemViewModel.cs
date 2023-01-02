using SkiaSharp;

namespace Pix2d.ViewModels.Color
{
    public class ColorItemViewModel
    {
        public ColorItemViewModel(SKColor color)
        {
            Color = color;
        }

        public SKColor Color { get; set; }
    }
}