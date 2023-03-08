namespace Pix2d.Plugins.Drawing.ViewModels;

public class FontItemViewModel
{
    public FontItemViewModel(string fontName)
    {
        Name = fontName;
    }

    public string Name { get; set; }
}