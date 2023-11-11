using Foundation;
using Avalonia;
using Avalonia.iOS;

namespace Pix2d.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<EditorApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            //.WithInterFont()
            ;
    }
}
