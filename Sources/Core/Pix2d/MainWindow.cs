using Pix2d.UI.Resources;

namespace Pix2d;

public class MainWindow : Window
{

    public MainWindow()
    {
        Title = "Pix2d Ultimate";
        var iconStream = StaticResources.AppIcon;
        Icon = new WindowIcon(iconStream);
        Width = 800;
        Height = 600;
#if DEBUG
        Topmost = false;
#endif
    }
}