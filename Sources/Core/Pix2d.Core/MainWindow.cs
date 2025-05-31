using Pix2d.UI.Resources;

namespace Pix2d;

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Pix2d Ultimate";
        var iconStream = ResourceManager.GetAsset("/Assets/app1.png");
        Icon = new WindowIcon(iconStream);
        Width = 800;
        Height = 600;
        FontFamily = StaticResources.Fonts.DefaultTextFontFamily;
#if DEBUG
        Topmost = false;
#endif
    }
}