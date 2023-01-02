namespace Pix2d;

public class MainWindow : Window
{

    public MainWindow()
    {
        Title = "Pix2d Ultimate";
        var iconStream = ViewBase.GetAsset("avares://Pix2d.Editor/Assets/app1.png");
        Icon = new WindowIcon(iconStream);
        Width = 800;
        Height = 600;
#if DEBUG
//        Topmost = true;
#endif
    }
}