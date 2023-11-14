using Pix2d.Abstract.Services;
using System.Threading.Tasks;
using Pix2d.Android;
using UIKit;

namespace Pix2d.iOS;

public class Application
{
    // This is the main entry point of the application.
    static void Main(string[] args)
    {
        EditorApp.Pix2dBootstrapper = new IOSPix2dBootstrapper();
        EditorApp.OnAppStarted = OnAppStarted;
        EditorApp.OnAppClosing = OnAppClosing;
        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }

    private static bool OnAppClosing()
    {
        var ss = CommonServiceLocator.ServiceLocator.Current.GetInstance<ISessionService>();
        if (ss != null)
        {
            Task.Run(() => ss.SaveSessionAsync()).GetAwaiter().GetResult();
        }
        return true;
    }

    private static void OnAppStarted(object obj)
    {

    }
}
