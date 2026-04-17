using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.UI;

namespace Pix2d.Droid;

[Application]
public class Pix2dApplication : AvaloniaAndroidApplication<EditorApp>
{
    private static readonly ServiceCollection ServiceCollection = [];

    internal static AndroidPix2dBootstrapper Bootstrapper { get; } = CreateBootstrapper();

    protected Pix2dApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        var serviceProvider = Bootstrapper.GetServiceProvider();

        return base.CustomizeAppBuilder(builder)
            .UseServiceProvider(serviceProvider)
            .UseComponentControlFactory(type => (Control)ActivatorUtilities.CreateInstance(serviceProvider, type))
            .UseViewInitializationStrategy(ViewInitializationStrategy.Immediate)
            .WithInterFont();
    }

    private static AndroidPix2dBootstrapper CreateBootstrapper()
    {
        var bootstrapper = new AndroidPix2dBootstrapper();
        bootstrapper.ConfigureServices(ServiceCollection);

        EditorApp.Pix2dBootstrapper = bootstrapper;
        EditorApp.UiModule ??= new UiModule();

        return bootstrapper;
    }
}
