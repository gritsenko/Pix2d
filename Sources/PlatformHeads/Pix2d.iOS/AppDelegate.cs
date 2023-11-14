using Foundation;
using Avalonia;
using Avalonia.iOS;
using Pix2d.Android;
using Pix2d.Abstract.Services;
using System.Threading.Tasks;
using Avalonia.Markup.Declarative;
using System;

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
            .UseServiceProvider(DefaultServiceLocator.ServiceLocatorProvider())
            //.WithInterFont()
            ;
    }
}
