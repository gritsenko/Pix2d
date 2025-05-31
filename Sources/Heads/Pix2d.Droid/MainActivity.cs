using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using Avalonia.Markup.Declarative;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.UI;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Pix2d.Droid;

[Activity(
    Label = "Pix2d",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    TaskAffinity = "",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]

[IntentFilter([Intent.ActionOpenDocument], Categories = [Intent.CategoryOpenable, Intent.CategoryDefault])]
[IntentFilter([Intent.ActionGetContent], Categories = [Intent.CategoryOpenable, Intent.CategoryDefault])]
public partial class MainActivity : AvaloniaMainActivity<EditorApp>
{
    public static Android.Net.Uri? PendingFileUri { get; set; }
    internal static MainActivity Instance { get; private set; } = null!;

    public event EventHandler<IFileContentSource?>? FileOpened;
    private const int ReadRequestCode = 42;
    private Android.Net.Uri? _uriAwaitingSafPermission;
    private bool _appCreated = false;
    private readonly AndroidPix2dBootstrapper _bootstrapper;
    private static readonly ServiceCollection ServiceCollection = [];

    public MainActivity()
    {
        Instance = this;
        _bootstrapper = new AndroidPix2dBootstrapper();
        _bootstrapper.ConfigureServices(ServiceCollection);
        EditorApp.Pix2dBootstrapper = _bootstrapper;
        EditorApp.UiModule ??= new UiModule();
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        OnBackPressedDispatcher.AddCallback(this, new BackPress(this));
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .UseServiceProvider(ServiceCollection.BuildServiceProvider())
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        if (PendingFileUri != null) 
            _bootstrapper.StartupDocument = PendingFileUri.ToString();
        
        base.OnCreate(savedInstanceState);

        if (Avalonia.Application.Current is EditorApp app) 
            app.UpdateTopLevelFromHostView();

        HideSystemUI();

        _appCreated = true;
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent?.Data != null && (intent.Action == Intent.ActionView || intent.Action == Intent.ActionOpenDocument || intent.Action == Intent.ActionGetContent))
        {
            System.Diagnostics.Debug.WriteLine($"MainActivity OnNewIntent: Received URI {intent.Data}");
            HandleIncomingUri(intent.Data);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"MainActivity OnNewIntent: Received intent with no data or unhandled action.");
        }
    }

    private void HideSystemUI()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // Android 11+
        {
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
            var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
            if (controller != null)
            {
                controller.Hide(WindowInsetsCompat.Type.SystemBars());
                controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            }
        }
        else // Старые версии (до Android 11)
        {
#pragma warning disable CS0618 // Отключаем предупреждение об устаревшем API
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky |
                SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen |
                SystemUiFlags.HideNavigation |
                SystemUiFlags.Fullscreen
            );
#pragma warning restore CS0618
        }

        // if (SupportActionBar != null) SupportActionBar.Hide();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            HideSystemUI();
        }
    }

    //public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    //{
    //    Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    //    base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    //}

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
    {
        var newExc = new Exception("TaskSchedulerOnUnobservedTaskException", unobservedTaskExceptionEventArgs.Exception);
        LogUnhandledException(newExc);
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
    {
        var newExc = new Exception("CurrentDomainOnUnhandledException", unhandledExceptionEventArgs.ExceptionObject as Exception);
        LogUnhandledException(newExc);
    }

    internal static void LogUnhandledException(Exception exception)
    {
        try
        {
            const string errorFileName = "Fatal.log";
            var libraryPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal); // iOS: Environment.SpecialFolder.Resources
            var errorFilePath = Path.Combine(libraryPath, errorFileName);
            var errorMessage = String.Format("Time: {0}\r\nError: Unhandled Exception\r\n{1}",
                DateTime.Now, exception.ToString());
            File.WriteAllText(errorFilePath, errorMessage);

            // Log to Android Device Logging.
            //Android.Util.Log.Error("Crash Report", errorMessage);
        }
        catch
        {
            // just suppress any error logging exceptions
        }
    }
}