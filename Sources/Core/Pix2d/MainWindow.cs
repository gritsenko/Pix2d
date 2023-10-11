using Avalonia.Media.Imaging;

namespace Pix2d;

public class MainWindow : Window
{
    public static Bitmap AppIcon => new(ResourceManager.GetAsset("/Assets/app1.png"));

    public MainWindow()
    {
        Title = "Pix2d Ultimate";
        var iconStream = AppIcon;
        Icon = new WindowIcon(iconStream);
        Width = 800;
        Height = 600;
#if DEBUG
        Topmost = false;
#endif
    }
}