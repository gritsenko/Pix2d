using Avalonia.Interactivity;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.MainMenu;

public class InfoView : ComponentBase
{
    private string _projectName;
    [Inject] public IMessenger Messenger { get; set; }
    [Inject] public AppState AppState { get; set; }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ProjectName = AppState.CurrentProject.Title;
        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
        Messenger.Register<ProjectSavedMessage>(this, OnProjectSaved);

        CoreServices.LicenseService.LicenseChanged += OnLicenseChanged;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Messenger.Unregister<ProjectLoadedMessage>(this, OnProjectLoaded);
        Messenger.Unregister<ProjectSavedMessage>(this, OnProjectSaved);
        
        CoreServices.LicenseService.LicenseChanged -= OnLicenseChanged;
    }

    private void OnLicenseChanged(object? sender, EventArgs e)
    {
        UpdateLicense();
    }

    private void UpdateLicense()
    {
        License = CoreServices.LicenseService.License.ToString();
    }

    private void OnProjectSaved(ProjectSavedMessage _)
    {
        UpdateProjectName();
    }

    private void OnProjectLoaded(ProjectLoadedMessage _)
    {
        UpdateProjectName();
    }

    private void UpdateProjectName()
    {
        ProjectName = AppState.CurrentProject.Title;
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            _projectName = value;
            OnPropertyChanged();
        }
    }

    private string _license;
    public string License
    {
        get => _license;
        set
        {
            _license = value;
            OnPropertyChanged();
        }
    }

    public InfoView()
    {
        UpdateLicense();
    }

    protected override object Build() =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).HorizontalAlignment(HorizontalAlignment.Center).Children(
                new Image().Source(StaticResources.UltimateImage).Width(128).Height(128).Margin(new Thickness(0, 0, 0, 16)),
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .FontSize(32)
                    .Text($"Pix2d v{CoreServices.PlatformStuffService.GetAppVersion()}"),
                new Grid().Rows("32,32").Cols("*,Auto").Width(256).Margin(new Thickness(0, 16)).Children(
                    new TextBlock().Text("Current project").VerticalAlignment(VerticalAlignment.Center),
                    new StackPanel().Col(1).Orientation(Orientation.Horizontal).Children(
                        new TextBlock().Col(1).Text(@ProjectName).VerticalAlignment(VerticalAlignment.Center),
                        new AppButton()
                            .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(new Thickness(8, 0, 0, 0))
                            .Width(24).Height(24).Content("\xE70F")
                            .Command(Commands.File.Rename)
                        ),
                    new TextBlock().Row(1).Text("License"),
                    new TextBlock().Row(1).Col(1).Text(@License)
                ),
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 0, 0, 8))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text("To share your art, suggestions or complains, please join us in Discord:"),
                new Button()
                    .Content("https://discord.gg/s4MpBVb")
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 0, 0, 24))
                    .OnClick(args =>
                    {
                        PlatformStuffService.OpenUrlInBrowser("https://discord.gg/s4MpBVb");
                    }),
                new KeyShortcutsView()
                    .IsVisible(PlatformStuffService.HasKeyboard)
            ));

    [Inject] IPlatformStuffService PlatformStuffService { get; set; } = null!;

}