namespace Pix2d.ViewModels.MainMenu
{
    public class NewDocumentSettingsPresetViewModel
    {
        public NewDocumentSettingsPresetViewModel(int width, int height)
        {
            Width = width;
            Height = height;

            Title = $"{Width}x{Height}";
        }

        public NewDocumentSettingsPresetViewModel()
        {
            Title = "Custom";
        }

        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}