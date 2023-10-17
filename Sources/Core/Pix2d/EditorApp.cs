using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
using Avalonia.VisualTree;
using CommonServiceLocator;
using Pix2d.UI;

namespace Pix2d;

public class EditorApp : Application
{
    public HostView HostView { get; private set; }

    public static IPix2dBootstrapper Pix2dBootstrapper { get; set; }
    public static Action<object> OnAppStarted { get; set; }
    public static Func<bool> OnAppClosing { get; set; }
    public static TopLevel TopLevel { get; private set; }
    public static IUiModule UiModule { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;

        ServiceLocator.SetLocatorProvider(DefaultServiceLocator.ServiceLocatorProvider);

        //AvaloniaXamlLoader.Load(this);
        InitResources();
        InitStyles();
    }

    private void InitResources()
    {

        //Resources.Add("ColorPickerThumb",
        //    new ImageBrush()
        //    {
        //        Source = StaticResources.ColorThumb 
        //    });
    }
    //FluentTheme GetFluentTheme() =>
    //    new(new Uri($"avares://{System.Reflection.Assembly.GetExecutingAssembly().GetName()}"))
    //        { Mode = FluentThemeMode.Dark };

    private void InitStyles()
    {
        try
        {
            //this.Styles.Add(GetFluentTheme());
            this.Styles.Add(new SimpleTheme());

            var styles = UiModule.GetStyles() as Styles;
            foreach (var externalStyle in styles)
            {
                this.Styles.Add(externalStyle);
            }

            foreach (var resource in styles.Resources)
            {
                this.Resources.Add(resource);
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
        HostView = new HostView();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) //DESKTOP
        {
            desktop.MainWindow = new MainWindow()
            {
                Content = HostView
            };
            OnAppStarted?.Invoke(desktop.MainWindow);
            TopLevel = desktop.MainWindow.GetVisualRoot() as TopLevel;
            desktop.MainWindow.Closing += (sender, args) =>
            {
                if (OnAppClosing == null) return;
                var close = OnAppClosing.Invoke();
                if (close == false) args.Cancel = true;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime) //WEB ASSEMBLY
        {
            singleViewLifetime.MainView = HostView;
            var root = singleViewLifetime.MainView.GetVisualRoot();
            TopLevel = root as TopLevel;
        }


        base.OnFrameworkInitializationCompleted();

        if (Design.IsDesignMode)
        {
            return;
        }
        InitializePix2d(HostView);
    }

    private async void InitializePix2d(HostView hostView)
    {
        try
        {
            if (EditorApp.Pix2dBootstrapper == null)
            {
                throw new NullReferenceException("Bootstrapper not set");
            }

            await EditorApp.Pix2dBootstrapper.InitializeAsync();

            hostView.LoadMainView();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
            throw;
        }
    }

    /// <summary>
    /// Used to set top level on android application on main activity
    /// </summary>
    /// <param name="topLevel"></param>
    public void UpdateTopLevelFromHostView()
    {
        TopLevel = HostView.GetVisualRoot() as TopLevel;
    }
}