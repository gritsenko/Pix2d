using Android.App;
using Android.Content;
using Android.OS;
using Avalonia;
using Application = Android.App.Application;

namespace Pix2d.AvaloniaAndroid
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (Avalonia.Application.Current == null)
            {
                App.Pix2dBootstrapper = new Pix2dBootstrapper();
                AppBuilder.Configure<App>()
                    .UseAndroid()
                    .SetupWithoutStarting();
            }

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }
    }
}