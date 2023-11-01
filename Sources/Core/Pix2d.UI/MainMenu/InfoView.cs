using Pix2d.Messages;
using Pix2d.UI.Common;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.MainMenu;

public class InfoView : ComponentBase
{
    [Inject] public IMessenger Messenger { get; set; }
    [Inject] public AppState AppState { get; set; }
    [Inject] IPlatformStuffService PlatformStuffService { get; set; } = null!;

    protected override void OnAfterInitialized()
    {
        AppState.WatchFor(x => x.LicenseType, StateHasChanged);
        AppState.WatchFor(x => x.CurrentProject, StateHasChanged);
        AppState.WatchFor(x => x.CurrentProject.File, StateHasChanged);

        Messenger.Register(this, (ProjectLoadedMessage msg) => StateHasChanged());
        Messenger.Register(this, (ProjectSavedMessage msg) => StateHasChanged());
    }

    protected override object Build() =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).HorizontalAlignment(HorizontalAlignment.Center).Children(
                new Image().Source(StaticResources.UltimateImage).Width(128).Height(128)
                    .Margin(new Thickness(0, 0, 0, 16)),
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .FontSize(32)
                    .Text(() => $"Pix2d v{PlatformStuffService.GetAppVersion()}"),
                new Grid().Rows("32,32").Cols("*,Auto").Width(256).Margin(new Thickness(0, 16)).Children(
                    new TextBlock().Text("Current project").VerticalAlignment(VerticalAlignment.Center),
                    
                    new StackPanel().Col(1).Orientation(Orientation.Horizontal)._Children(new()
                    {
                        new TextBlock().Col(1).Text(() => AppState.CurrentProject.Title)
                            .VerticalAlignment(VerticalAlignment.Center),

                        new AppButton()
                            .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(new Thickness(8, 0, 0, 0))
                            .Width(24).Height(24).Content("\xE70F")
                            .Command(Commands.File.Rename)
                    }),

                    new TextBlock().Row(1).Text("License"),
                    
                    new TextBlock().Row(1).Col(1).Text(() => AppState.LicenseType.ToString())
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
                    .OnClick(args => { PlatformStuffService.OpenUrlInBrowser("https://discord.gg/s4MpBVb"); }),
                new KeyShortcutsView()
                    .IsVisible(PlatformStuffService.HasKeyboard)
            ));


}