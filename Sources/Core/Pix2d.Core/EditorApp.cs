using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
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
            hostView.LoadMainView(UiModule!.GetMainViewType(), Pix2dBootstrapper!.GetServiceProvider());
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            Logger.Log(ex.StackTrace!);
            throw;
        }
        AppInitialized?.Invoke(this);
    }
}
