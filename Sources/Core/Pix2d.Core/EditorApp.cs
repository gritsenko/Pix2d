using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Services;
using Pix2d.Primitives.Crash;
using Pix2d.UI;

namespace Pix2d;

public class EditorApp : Application
{
    public HostView? HostView { get; private set; }

    public static IPix2dBootstrapper? Pix2dBootstrapper { get; set; }
    public static Action<object>? AppStarted { get; set; }
    public static Action<EditorApp>? AppInitialized { get; set; }
    public static Func<bool>? OnAppClosing { get; set; }
    public static TopLevel? TopLevel { get; private set; }
    public static IUiModule? UiModule { get; set; }

    /// <summary>
    /// Used to set top level on android application on main activity
    /// </summary>
    public void UpdateTopLevelFromHostView() => TopLevel = HostView == null ? null : TopLevel.GetTopLevel(HostView);

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        InitStyles();
    }

    private void InitStyles()
    {
        try
        {
            Styles.Add(new SimpleTheme());

            var styles = (Styles?)UiModule?.GetStyles();
            if (styles != null)
            {
                foreach (var externalStyle in styles)
                    Styles.Add(externalStyle);

                foreach (var resource in styles.Resources)
                    Resources.Add(resource);
            }

        }
        catch (Exception ex)
        {
            //can't load system theme
            Console.WriteLine("CRAP! No styles! " + ex.Message);
        }

    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            //DESKTOP
            case IClassicDesktopStyleApplicationLifetime desktop:
                InitDesktopWindow(desktop);
                break;
            //ANDROID
            case IActivityApplicationLifetime activityLifetime:
                activityLifetime.MainViewFactory = CreateMainView;
                break;
            //WEB ASSEMBLY
            case ISingleViewApplicationLifetime singleViewLifetime:
                {
                    HostView = new HostView();
                    singleViewLifetime.MainView = HostView;
                    TopLevel = TopLevel.GetTopLevel(singleViewLifetime.MainView);
                    break;
                }
        }

        base.OnFrameworkInitializationCompleted();
        EnsurePix2dInitialized();

        if (ApplicationLifetime is not IActivityApplicationLifetime)
            AttachMainView(HostView);

    }

    private Control CreateMainView()
    {
        HostView = new HostView();
        AttachMainView(HostView);
        return HostView;
    }

    private void InitDesktopWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        HostView = new HostView();
        desktop.MainWindow = new MainWindow()
        {
            Content = HostView
        };
        TopLevel = desktop.MainWindow;
        desktop.MainWindow.Closing += (sender, args) =>
        {
            if (OnAppClosing == null) return;
            var close = OnAppClosing.Invoke();
            if (close == false) args.Cancel = true;
        };
        AppStarted?.Invoke(desktop.MainWindow);
    }

    private bool _pix2dInitialized;
    private bool _localeReloadSubscribed;

    private void EnsurePix2dInitialized()
    {
        if (_pix2dInitialized || Design.IsDesignMode)
            return;

        if (Pix2dBootstrapper == null)
            throw new NullReferenceException("Bootstrapper not set");

        Pix2dBootstrapper.Initialize();
        _pix2dInitialized = true;
    }

    private void AttachMainView(HostView? hostView)
    {
        if (Design.IsDesignMode)
            return;

        if (hostView == null)
            return;

        try
        {
            EnsurePix2dInitialized();
            var serviceProvider = Pix2dBootstrapper!.GetServiceProvider();
            hostView.LoadMainView(UiModule!.GetMainViewType(), serviceProvider);
            SubscribeToLocaleChanges(hostView, serviceProvider);

            // Main view loaded — surface the pending crash report and/or the first-launch telemetry
            // consent prompt.
            TryShowStartupTelemetryDialog(serviceProvider);
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            Logger.Log(ex.StackTrace!);
            try
            {
                Pix2dBootstrapper?.GetServiceProvider().GetService<ICrashReportService>()?
                    .CaptureFatal(ex, "EditorApp.AttachMainView");
            }
            catch
            {
            }
            throw;
        }
        AppInitialized?.Invoke(this);
    }

    private static void TryShowStartupTelemetryDialog(IServiceProvider serviceProvider)
    {
        try
        {
            var crashService = serviceProvider.GetService<ICrashReportService>();
            var dialogService = serviceProvider.GetService<IDialogService>();
            var platform = serviceProvider.GetService<IPlatformStuffService>();
            if (crashService == null || dialogService == null || platform == null)
                return;

            // Crash telemetry (the Sentry sink) exists on Android + the desktop family (Windows /
            // Linux / macOS, incl. the MS Store bundle); anonymous usage analytics (AppStat)
            // additionally runs on WASM. Consent is relevant wherever *any* of them can run.
            var isCrashSinkPlatform = platform.CurrentPlatform is PlatformType.Android
                or PlatformType.WindowsDesktop
                or PlatformType.CrossPlatformDesktop
                or PlatformType.MacOS
                or PlatformType.WindowsStore;
            var isTelemetryPlatform = isCrashSinkPlatform || platform.CurrentPlatform == PlatformType.WASM;
            if (!isTelemetryPlatform)
                return;

            // A genuine pending crash wins: its dialog already collects consent (via its toggle) while
            // consent is still Unset, so we never stack the standalone consent prompt on top of it.
            // Auto-show stays on crash-sink platforms only — WASM keeps crash reports manual-only.
            if (isCrashSinkPlatform && crashService.HasPendingCrashReport)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        _ = dialogService.ShowDialogAsync(
                            new UI.Dialogs.CrashReportDialogView(crashService, platform));
                    }
                    catch
                    {
                    }
                });
                return;
            }

            // No crash to show — ask for telemetry consent once, on first launch (consent still Unset).
            // Strict opt-in: nothing is sent until the user allows it here. Covers WASM too, so browser
            // analytics is no longer silently disabled under the opt-in model.
            if (crashService.TelemetryConsent == TelemetryConsent.Unset)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        _ = dialogService.ShowDialogAsync(
                            new UI.Dialogs.TelemetryConsentDialogView(crashService));
                    }
                    catch
                    {
                    }
                });
            }
        }
        catch
        {
        }
    }

    private void SubscribeToLocaleChanges(HostView hostView, IServiceProvider serviceProvider)
    {
        if (_localeReloadSubscribed)
            return;

        var localizationService = serviceProvider.GetService<ILocalizationService>();
        if (localizationService == null)
            return;

        localizationService.LocaleChanged += () =>
            Dispatcher.UIThread.Post(() => hostView.LoadMainView(UiModule!.GetMainViewType(), serviceProvider));

        _localeReloadSubscribed = true;
    }
}
