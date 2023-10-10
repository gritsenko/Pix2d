using Pix2d.UI.Resources;

namespace Pix2d.UI.MainMenu;

public class InfoView : ComponentBase
{
    protected override object Build() =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).HorizontalAlignment(HorizontalAlignment.Center).Children(
                new Image().Source(StaticResources.UltimateImage).Width(128).Height(128).Margin(new Thickness(0, 0, 0, 16)),
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .FontSize(32)
                    .Text($"Pix2d v{CoreServices.PlatformStuffService.GetAppVersion()}"),
                new Grid().Rows("24,24").Cols("*,Auto").Width(256).Margin(new Thickness(0, 16)).Children(
                    new TextBlock().Text("Current project"),
                    new TextBlock().Col(1).Text(Pix2DApp.Instance.AppState.CurrentProject.Title),
                    new TextBlock().Row(1).Text("License"),
                    new TextBlock().Row(1).Col(1).Text(Pix2DApp.Instance.CurrentLicense)
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