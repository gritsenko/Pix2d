namespace Pix2d;

public class MainWindow : Window
{

    public MainWindow()
    {
        Title = "Pix2d Ultimate";
        var iconStream = ViewBase.GetAsset(StaticResources.GetEmbeddedResourceURI("/Assets/app1.png").ToString());
        Icon = new WindowIcon(iconStream);
        Width = 800;
        Height = 600;
#if DEBUG
        Topmost = false;
#endif
    }
}