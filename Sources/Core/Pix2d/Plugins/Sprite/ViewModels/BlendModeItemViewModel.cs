using System.ComponentModel;
using SkiaSharp;

namespace Pix2d.ViewModels.Layers
{
    [Bindable(true)]
    public class BlendModeItemViewModel
    {
        public BlendModeItemViewModel(SKBlendMode blendMode, string title = null)
        {
            BlendMode = blendMode;

            Title = title ?? BlendMode.ToString();
        }

        public string Title { get; set; }

        public SKBlendMode BlendMode { get; set; }
    }
}