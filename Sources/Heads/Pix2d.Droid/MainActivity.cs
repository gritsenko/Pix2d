﻿using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Declarative;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.UI;
using System;
using System.IO;
using System.Linq;
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
            .UseServiceProvider(_bootstrapper.GetServiceProvider())
            .UseViewInitializationStrategy(ViewInitializationStrategy.Immediate)
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

        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View v, WindowInsetsCompat insets)
        {
            var systemBars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            var displayCutout = insets.GetInsets(WindowInsetsCompat.Type.DisplayCutout());
            
            // Combine system bars and display cutout insets
            var topInset = Math.Max((int)systemBars.Top, (int)displayCutout.Top);
            var leftInset = Math.Max((int)systemBars.Left, (int)displayCutout.Left);
            var rightInset = Math.Max((int)systemBars.Right, (int)displayCutout.Right);
            var bottomInset = Math.Max((int)systemBars.Bottom, (int)displayCutout.Bottom);
            
            _activity.ApplySafeAreaInsets(leftInset, topInset, rightInset, bottomInset);
            
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
}