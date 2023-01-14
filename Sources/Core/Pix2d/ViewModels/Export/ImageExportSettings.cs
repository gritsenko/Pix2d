using SkiaSharp;

namespace Pix2d.ViewModels.Export
{
    public class ImageExportSettings
    {
        public int Scale { get; set; } = 1;

        //public bool UseBackgroundColor { get; set; }

        public int SpritesheetColumns { get; set; }

        public string DefaultFileName { get; set; }
    }
}