using Pix2d.Command;
using Pix2d.Messages;
using Pix2d.UI.Common;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.UI.MainMenu;

public class InfoView : LocalizedComponentBase
{
    protected override object Build() =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).HorizontalAlignment(HorizontalAlignment.Center)
                .MaxWidth(360)
                .Children(
                new Image().Source(StaticResources.UltimateImage).Width(128).Height(128)
                    .Margin(new Thickness(0, 0, 0, 16)),
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .FontSize(32)
                    .Text(() => $"Pix2d v{PlatformStuffService.GetAppVersion()}"),
                new Grid().Rows("32,32").Cols("*,Auto").Width(256).Margin(new Thickness(0, 16)).Children(
                    new TextBlock().Text("Current project").VerticalAlignment(VerticalAlignment.Center),

                    new StackPanel().Col(1).Orientation(Orientation.Horizontal)._Children([
                        new TextBlock().Col(1).Text(() => AppState.CurrentProject?.Title ?? "No project")
                            .VerticalAlignment(VerticalAlignment.Center),


                        new AppButton()
                            .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(new Thickness(8, 0, 0, 0))
                            .Width(24).Height(24).Content("\xE70F")
                            .Label("")
                            .Command(FileCommands.Rename)
                    ])

                ),
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 0, 0, 8))
                    .TextWrapping(TextWrapping.Wrap)
                    .FontSize(16)
                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                    .Text(L("To share your art, suggestions or complains, please join us in:")),

                new StackPanel().Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(

                    new Button()
                        .FontSize(14)
                        .Classes("btn").Classes("btn-bright")
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .Height(40)
                        .Margin(new Thickness(6, 0, 6, 24))
                        .OnClick(args => { PlatformStuffService.OpenUrlInBrowser("https://t.me/pix2dApp"); })
                        .Content(
                            new StackPanel().Orientation(Orientation.Horizontal).Children(
                                new Path()
                                    .Data(StaticResources.Icons.TelegramIcon)
                                    .Fill(Brushes.White)
                                    .Width(24)
                                    .Height(24)
                                    .Margin(12, 4, 0, 0)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Stretch(Stretch.Uniform),
                                new TextBlock()
                                    .Text(L("TELEGRAM"))
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(12, 0)
                            )
                        )
                ),

                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Text(L("Choose UI language:"))
                    .FontSize(20)
                    .Margin(bottom: 10)
                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                new ComboBox()
                    .ItemsSource(AppState.AvailableLocales)
                    .SelectedItem(() => AppState.Locale, v =>
                    {
                        var value = v as string;
                        if (value != null && !AppState.Locale.Equals(value))
                            LocalizationService.SetLocale(value);
                    })
                    .Margin(0, 0, 0, 12),

                new StackPanel().Orientation(Orientation.Horizontal)
                    .Children(
                        new TextBlock().Text(L("UI Scale:")).Margin(0, 16, 0, 10).FontSize(20).VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),

                        new TextBlock()
                            .Text(() => $"{AppState.UiScale}x").Margin(10, 20, 0, 10).FontSize(18).VerticalAlignment(VerticalAlignment.Center)
                    ),

                new Grid().Rows("32").Cols("*, 100,100").Margin(0, 0, 0, 12).Children(
                    new Slider().Col(0)
                        .Minimum(0.5)
                        .Maximum(2.5)
                        .TickFrequency(0.25)
                        .IsSnapToTickEnabled(true)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Value(() => AppState.UiScale, v => AppState.UiScale = v),
                    new Button()
                        .Col(1)
                        .Classes("btn").Classes("btn-bright")
                        .Margin(8, 0, 0, 0)
                        .OnClick(_ => ApplyScale())
                        .Content(L("Apply")),
                    new Button()
                        .Col(2)
                        .Classes("btn").Classes("btn-bright")
                        .Margin(8, 0, 0, 0)
                        .OnClick(_ => ResetScale())
                        .Content(L("Reset"))
                ),

                new TextBlock().Text(L("Keyboard shortcuts:")).Margin(0, 0, 0, 16).FontSize(20)
                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),

                new KeyShortcutsView()
                    .IsVisible(PlatformStuffService.HasKeyboard)
            ));

    [Inject] public IMessenger Messenger { get; set; } = null!;
    [Inject] public AppState AppState { get; set; } = null!;
    [Inject] private ILocalizationService LocalizationService { get; set; } = null!;
    [Inject] IPlatformStuffService PlatformStuffService { get; set; } = null!;
    [Inject] private IUiScaleService UiScaleService { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;

    private FileCommands FileCommands => CommandService.GetCommandList<FileCommands>()!;

    protected override void OnAfterInitialized()
    {
        AppState.WatchFor(x => x.LicenseType, StateHasChanged);
        AppState.WatchFor(x => x.CurrentProject, StateHasChanged);
        AppState.WatchFor(x => x.CurrentProject.File, StateHasChanged);

        Messenger.Register(this, (ProjectLoadedMessage msg) => StateHasChanged());
        Messenger.Register(this, (ProjectSavedMessage msg) => StateHasChanged());
    }

    private void ApplyScale()
    {
        UiScaleService.SetUiScale(AppState.UiScale);
    }

    private void ResetScale()
    {
        AppState.UiScale = 1.0;
        ApplyScale();
    }

}