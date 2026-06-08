using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
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
        // Managed exceptions crossing the Java↔managed boundary on Android (e.g. on the UI thread
        // during startup) are delivered HERE, not reliably through AppDomain.UnhandledException.
        // Without this hook those crashes are invisible to the crash service and surface next launch
        // as an empty "previous launch did not finish" report.
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironmentOnUnhandledExceptionRaiser;
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

    private static void AndroidEnvironmentOnUnhandledExceptionRaiser(object? sender, Android.Runtime.RaiseThrowableEventArgs e)
    {
        var newExc = new Exception("AndroidEnvironment.UnhandledExceptionRaiser", e.Exception);
        LogUnhandledException(newExc);
        // Intentionally leave e.Handled = false: the exception is fatal and the app state may be
        // corrupt. We only want the report captured before the process goes down — not to limp on.
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
        // Reaching OnPause means the UI came up and the app is interactive, so the launch
        // succeeded for crash-detection purposes. Clearing the in-progress marker here prevents
        // a phantom "previous launch did not finish" report when Android later kills the process
        // in the background. A genuine crash mid-session is still caught via process-exit info.
        MarkLaunchCompletedSafely();
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

    private static void MarkLaunchCompletedSafely()
    {
        try
        {
            if (EditorApp.Pix2dBootstrapper?.GetServiceProvider() is not { } sp)
                return;

            var crashService = sp.GetService(typeof(Pix2d.Abstract.Services.ICrashReportService))
                as Pix2d.Abstract.Services.ICrashReportService;
            crashService?.MarkLaunchCompleted();
        }
        catch
        {
        }
    }

    // Called from the deliberate double-back exit just before the process is terminated. Persists a
    // marker so the next launch doesn't mistake the self-kill (reported by the OS as SIGNALED) for a
    // native crash and pop a phantom crash report.
    internal static void MarkCleanExitSafely()
    {
        try
        {
            if (EditorApp.Pix2dBootstrapper?.GetServiceProvider() is not { } sp)
                return;

            var crashService = sp.GetService(typeof(Pix2d.Abstract.Services.ICrashReportService))
                as Pix2d.Abstract.Services.ICrashReportService;
            crashService?.MarkCleanExit();
        }
        catch
        {
        }
    }

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
