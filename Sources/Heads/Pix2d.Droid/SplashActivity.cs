using System;
using Android.App;
using Android.Content;
using Android.OS;
using Application = Android.App.Application;
using Avalonia;
using Avalonia.Android;
using Avalonia.Markup.Declarative;

namespace Pix2d.Android;

[Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
public class SplashActivity : AvaloniaSplashActivity<EditorApp> {
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) {
        return base.CustomizeAppBuilder(builder)
                   .UseServiceProvider(DefaultServiceLocator.ServiceLocatorProvider());
    }

    protected override void OnCreate(Bundle? savedInstanceState) {
        EditorApp.Pix2dBootstrapper ??= new AndroidPix2dBootstrapper();

        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume() {
        base.OnResume();


        if (Avalonia.Application.Current == null) {
            AppBuilder.Configure<EditorApp>()
                .UseAndroid()
                .SetupWithoutStarting();
        }
        StartActivity(new Intent(Application.Context, typeof(MainActivity)));
    }
}
