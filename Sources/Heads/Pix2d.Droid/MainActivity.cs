using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform.FileSystem;
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
public partial class MainActivity : AvaloniaMainActivity
{
    public static Android.Net.Uri? PendingFileUri { get; set; }
    internal static MainActivity? Instance { get; private set; }

    public event EventHandler<IFileContentSource?>? FileOpened;
    private const int ReadRequestCode = 42;
    private Android.Net.Uri? _uriAwaitingSafPermission;
    private bool _appCreated = false;

    public MainActivity()
    {
        Instance = this;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        OnBackPressedDispatcher.AddCallback(this, new BackPress(this));
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        SetTheme(Resource.Style.MyTheme_NoActionBar);

        if (PendingFileUri != null)
            Pix2dApplication.Bootstrapper.StartupDocument = PendingFileUri.ToString();

        base.OnCreate(savedInstanceState);

        AttachPlatformServices();

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
        var window = Window;
        if (window?.DecorView != null)
        {
            WindowCompat.SetDecorFitsSystemWindows(window, false);
            var controller = WindowCompat.GetInsetsController(window, window.DecorView);
            if (controller != null)
            {
                controller.Hide(WindowInsetsCompat.Type.SystemBars());
                controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            }
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
        // Preferred path: route through ICrashReportService so the report ends up in the shared
        // CrashReports folder and the bootstrapper-level handlers see consistent state.
        try
        {
            var sp = EditorApp.Pix2dBootstrapper?.GetServiceProvider();
            var crashService = sp?.GetService(typeof(Pix2d.Abstract.Services.ICrashReportService))
                as Pix2d.Abstract.Services.ICrashReportService;
            if (crashService != null)
            {
                crashService.CaptureFatal(exception, "Android.PreBootstrap");
                return;
            }
        }
        catch
        {
        }

        // Last-resort plain text fallback: the bootstrapper isn't up yet and we have nowhere else to go.
        try
        {
            const string errorFileName = "Fatal.log";
            var libraryPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            var errorFilePath = Path.Combine(libraryPath, errorFileName);
            var errorMessage = String.Format("Time: {0}\r\nError: Unhandled Exception\r\n{1}",
                DateTime.Now, exception.ToString());
            File.WriteAllText(errorFilePath, errorMessage);
        }
        catch
        {
            // just suppress any error logging exceptions
        }
    }

    protected override void OnPause()
    {
        SaveSessionSafely();
        base.OnPause();
    }

    protected override void OnStop()
    {
        SaveSessionSafely();
        base.OnStop();
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;

        base.OnDestroy();
    }

    internal static bool TryGetInstance(out MainActivity activity)
    {
        activity = Instance!;
        return activity != null;
    }

    private static void AttachPlatformServices()
    {
        if (Pix2dApplication.Bootstrapper.GetServiceProvider().GetService(typeof(Pix2d.Droid.Services.AndroidPlatformStuffService)) is Pix2d.Droid.Services.AndroidPlatformStuffService platformStuff)
            platformStuff.AttachActivity(Instance!);
    }

    // Bounded wait for the lifecycle save. Android gives an app ~5 s after
    // onPause / onStop before it may freeze or tombstone the process, so we
    // stay safely under that. The actual save runs synchronously on this
    // (UI / Activity main) thread; only the file-I/O commit is offloaded.
    private static readonly TimeSpan LifecycleSaveTimeout = TimeSpan.FromSeconds(4);

    internal static void SaveSessionSafely()
    {
        try
        {
            // OnPause / OnStop and the explicit double-back exit can call this
            // back-to-back on the same UI thread. AutoSaveService.ForceSaveSync
            // coalesces via its commit lock — repeated calls during the same
            // transition are cheap no-ops. Importantly, the save runs inline on
            // the Activity main thread (drain + snapshot are UI-thread
            // operations) and only the file-I/O commit is offloaded.
            if (EditorApp.Pix2dBootstrapper?.GetServiceProvider() is not { } sp)
                return;

            var autoSave = sp.GetService<Pix2d.Abstract.Services.IAutoSaveService>();
            autoSave?.ForceSaveSync(LifecycleSaveTimeout);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("Pix2d", $"Error in SaveSessionSafely: {ex}");
        }
    }
}
