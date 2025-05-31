using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
using Avalonia.VisualTree;
using Pix2d.UI;

namespace Pix2d;

public class EditorApp : Application
{
    public HostView HostView { get; private set; }

    public static IPix2dBootstrapper Pix2dBootstrapper { get; set; }
    public static Action<object> AppStarted { get; set; }
    public static Action AppInitialized { get; set; }
    public static Func<bool> OnAppClosing { get; set; }
    public static TopLevel TopLevel { get; private set; }
    public static IUiModule UiModule { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        InitStyles();
    }

    /// <summary>
    /// Used to set top level on android application on main activity
    /// </summary>
    public void UpdateTopLevelFromHostView() => TopLevel = HostView.GetVisualRoot() as TopLevel;

    private void InitStyles()
    {
        try
        {
            Styles.Add(new SimpleTheme());

            var styles = (Styles)UiModule.GetStyles();
            foreach (var externalStyle in styles)
                Styles.Add(externalStyle);

            foreach (var resource in styles.Resources)
                Resources.Add(resource);
        }
        catch (Exception ex)
        {
            //can't load system theme
            Console.WriteLine("CRAP! No styles! " + ex.Message);
        }

    }

    public override void OnFrameworkInitializationCompleted()
    {
        HostView = new HostView();

        switch (ApplicationLifetime)
        {
            //DESKTOP
            case IClassicDesktopStyleApplicationLifetime desktop:
                InitDesktopWindow(desktop);
                break;
            //WEB ASSEMBLY
            case ISingleViewApplicationLifetime singleViewLifetime:
                {
                    singleViewLifetime.MainView = HostView;
                    var root = singleViewLifetime.MainView.GetVisualRoot();
                    TopLevel = root as TopLevel;
                    break;
                }
        }

        base.OnFrameworkInitializationCompleted();

        InitializePix2d(HostView);

    }

    private void InitDesktopWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow()
        {
            Content = HostView
        };
        TopLevel = desktop.MainWindow.GetVisualRoot() as TopLevel;
        desktop.MainWindow.Closing += (sender, args) =>
        {
            if (OnAppClosing == null) return;
            var close = OnAppClosing.Invoke();
            if (close == false) args.Cancel = true;
        };
        AppStarted?.Invoke(desktop.MainWindow);
    }

    private void InitializePix2d(HostView hostView)
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            if (Pix2dBootstrapper == null)
            {
                throw new NullReferenceException("Bootstrapper not set");
            }

            Pix2dBootstrapper.Initialize();

            hostView.LoadMainView(UiModule.GetMainViewType(), Pix2dBootstrapper.GetServiceProvider());
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            Logger.Log(ex.StackTrace);
            throw;
        }
        AppInitialized?.Invoke();
    }
}