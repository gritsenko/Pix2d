using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
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
    Theme = "@style/MyTheme.Splash",
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

    private static long _lastLifecycleSaveTicks;
    private static int _lifecycleSaveInFlight;

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
        var serviceProvider = _bootstrapper.GetServiceProvider();

        return base.CustomizeAppBuilder(builder)
            .UseServiceProvider(serviceProvider)
            .UseComponentControlFactory(type => (Control)ActivatorUtilities.CreateInstance(serviceProvider, type))
            .UseViewInitializationStrategy(ViewInitializationStrategy.Immediate)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        SetTheme(Resource.Style.MyTheme_NoActionBar);

        if (PendingFileUri != null)
            _bootstrapper.StartupDocument = PendingFileUri.ToString();

        base.OnCreate(savedInstanceState);

        if (Avalonia.Application.Current is EditorApp app)
            app.UpdateTopLevelFromHostView();

        HideSystemUI();
        SetupWindowInsetsListener();

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
            var window = Window;
            if (window != null)
            {
                WindowCompat.SetDecorFitsSystemWindows(window, false);
                var controller = WindowCompat.GetInsetsController(window, window.DecorView);
                if (controller != null)
                {
                    controller.Hide(WindowInsetsCompat.Type.SystemBars());
                    controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                }
            }
        }
        else // Старые версии (до Android 11)
        {
#pragma warning disable CS0618 // Отключаем предупреждение об устаревшем API
            var window = Window;
            if (window?.DecorView != null)
            {
                window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.ImmersiveSticky |
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.Fullscreen
                );
            }
#pragma warning restore CS0618
        }

        // if (SupportActionBar != null) SupportActionBar.Hide();
    }

    private void SetupWindowInsetsListener()
    {
        if (Window?.DecorView != null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(Window.DecorView, new WindowInsetsListener(this));
        }
    }

    private class WindowInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        private readonly MainActivity _activity;

        public WindowInsetsListener(MainActivity activity)
        {
            _activity = activity;
        }

        public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v == null || insets == null)
                return insets;

            var systemBars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            var displayCutout = insets.GetInsets(WindowInsetsCompat.Type.DisplayCutout());

            if (systemBars != null && displayCutout != null)
            {
                // Combine system bars and display cutout insets
                var topInset = Math.Max(systemBars.Top, displayCutout.Top);
                var leftInset = Math.Max(systemBars.Left, displayCutout.Left);
                var rightInset = Math.Max(systemBars.Right, displayCutout.Right);
                var bottomInset = Math.Max(systemBars.Bottom, displayCutout.Bottom);

                _activity.ApplySafeAreaInsets(leftInset, topInset, rightInset, bottomInset);
            }

            return insets;
        }
    }

    private void ApplySafeAreaInsets(int left, int top, int right, int bottom)
    {
        if (Avalonia.Application.Current is EditorApp app)
        {
            // Convert Android pixels to Avalonia device-independent pixels
            var density = Resources?.DisplayMetrics?.Density ?? 1f;
            var safeAreaMargin = new Thickness(
                left / density,
                top / density,
                right / density,
                bottom / density
            );

            // Apply margin to the HostView to offset its position once
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var hostView = app.HostView;
                if (hostView != null)
                {
                    //hostView.Margin = safeAreaMargin;
                    System.Diagnostics.Debug.WriteLine($"Applied SafeAreaMargin to HostView: L={safeAreaMargin.Left}, T={safeAreaMargin.Top}, R={safeAreaMargin.Right}, B={safeAreaMargin.Bottom}");
                }
            });
        }
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

    protected override void OnPause()
    {
        SaveSessionSafely(critical: true);
        base.OnPause();
    }

    protected override void OnStop()
    {
        SaveSessionSafely(critical: true);
        base.OnStop();
    }

    protected override void OnDestroy()
    {
        //SaveSessionSafely(critical: true);
        base.OnDestroy();
    }

    private void SaveSessionSafely(bool critical)
    {
        try
        {
            // OnPause -> OnStop -> OnDestroy can happen back-to-back. Don’t queue multiple saves.
            if (System.Threading.Interlocked.Exchange(ref _lifecycleSaveInFlight, 1) == 1)
                return;

            var now = DateTime.UtcNow.Ticks;
            var last = System.Threading.Interlocked.Read(ref _lastLifecycleSaveTicks);

            // Throttle to at most once per 2 seconds.
            if (last != 0 && new TimeSpan(now - last) < TimeSpan.FromSeconds(2))
            {
                System.Threading.Interlocked.Exchange(ref _lifecycleSaveInFlight, 0);
                return;
            }

            System.Threading.Interlocked.Exchange(ref _lastLifecycleSaveTicks, now);

            if (EditorApp.Pix2dBootstrapper?.GetServiceProvider() is not { } sp)
            {
                System.Threading.Interlocked.Exchange(ref _lifecycleSaveInFlight, 0);
                return;
            }

            var sessionService = sp.GetService<Pix2d.Abstract.Services.ISessionService>();
            if (sessionService is null)
            {
                System.Threading.Interlocked.Exchange(ref _lifecycleSaveInFlight, 0);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (critical)
                        await sessionService.ForceSaveAsync(TimeSpan.FromSeconds(3));
                    else
                        await sessionService.TrySaveSessionAsync();
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("Pix2d", $"Failed to save session: {ex}");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _lifecycleSaveInFlight, 0);
                }
            });
        }
        catch (Exception ex)
        {
            System.Threading.Interlocked.Exchange(ref _lifecycleSaveInFlight, 0);
            Android.Util.Log.Error("Pix2d", $"Error in SaveSessionSafely: {ex}");
        }
    }
}
