using Foundation;
using Avalonia;
using Avalonia.iOS;
using Pix2d.Android;
using Pix2d.Abstract.Services;
using System.Threading.Tasks;

namespace Pix2d.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<EditorApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        EditorApp.Pix2dBootstrapper = new IOSPix2dBootstrapper();
        EditorApp.OnAppStarted = OnAppStarted;
        EditorApp.OnAppClosing = OnAppClosing;

        return base.CustomizeAppBuilder(builder)
            //.WithInterFont()
            ;
    }

    private bool OnAppClosing()
    {
        var ss = CommonServiceLocator.ServiceLocator.Current.GetInstance<ISessionService>();
        if (ss != null)
        {
            Task.Run(() => ss.SaveSessionAsync()).GetAwaiter().GetResult();
        }
        return true;
    }

    private void OnAppStarted(object obj)
    {
        
    }
}
