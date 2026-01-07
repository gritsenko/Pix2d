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

                //new TextBlock().Row(1).Text(L("License")),

                //new TextBlock().Row(1).Col(1).Text(() => AppState.LicenseType.ToString())
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
                                    .Data(Geometry.Parse(TelegramIconPath))
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

    private readonly string DiscordIconPath =
    "M20.6438 5.10122C20.5623 4.99322 20.4533 4.90922 20.3278 4.85822L19.9698 4.71222C18.6058 4.15572 17.6208 3.75372 16.4488 3.53422C15.7443 3.40222 14.9843 3.80372 14.5953 4.51072C13.9538 4.45122 13.2753 4.43122 12.5208 4.45072C11.7373 4.43222 11.0528 4.45072 10.4103 4.51072C10.0273 3.81522 9.26976 3.42372 8.56426 3.55472C7.30176 3.79072 6.26226 4.21372 4.67776 4.85772C4.55226 4.90872 4.44326 4.99272 4.36176 5.10072C1.70026 8.62822 0.639756 12.4562 1.11976 16.8027C1.14576 17.0352 1.27826 17.2422 1.47876 17.3632C3.25476 18.4317 4.79326 19.1647 6.32126 19.6692C7.05826 19.9127 7.87676 19.6352 8.31476 18.9957L8.99776 17.9962C10.1208 18.2797 11.2913 18.4342 12.5033 18.4572C13.6848 18.4347 14.8503 18.2792 15.9743 17.9942L16.6943 19.0177C17.0298 19.4952 17.5623 19.7622 18.1198 19.7622C18.3038 19.7622 18.4903 19.7332 18.6733 19.6727C20.2043 19.1682 21.7463 18.4342 23.5268 17.3632C23.7268 17.2427 23.8598 17.0352 23.8858 16.8027C24.3653 12.4557 23.3048 8.62772 20.6438 5.10122ZM8.74376 13.9917C7.78076 13.9917 6.98826 12.9772 6.98826 11.7442C6.98826 10.5112 7.78076 9.49672 8.74376 9.49672C9.70676 9.49672 10.4993 10.5112 10.4993 11.7442C10.4993 12.9772 9.70676 13.9917 8.74376 13.9917ZM15.7438 14.0052C14.7898 14.0052 14.0048 12.9847 14.0048 11.7442C14.0048 10.5037 14.7898 9.48322 15.7438 9.48322C16.6978 9.48322 17.4828 10.5037 17.4828 11.7442C17.4828 12.9847 16.6978 14.0052 15.7438 14.0052Z";

    private readonly string TelegramIconPath =
        "M1.91512 7.80784C8.19912 5.04334 16.3311 4.45072 8.74376 19.1647 6.32126 4.43122 12.5208 4.45072C10.4103 4.51072 9.49672 10.0273 4.43122 11.0528 4.45072C10.4103 4.51072 10.0273 10.4103 10.4993 10.5112 14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.9743 17.9942L8.99776 17.9962C10.1208 18.2797 11.2913 18.4342 12.5033 18.4572C13.6848 18.4347 14.8503 18.2792 15.9743 17.9942ZM8.74376 13.9917C7.78076 13.9917 6.98826 12.9772 6.98826 11.7442C6.98826 10.5112 7.78076 9.49672 8.74376 9.49672C9.70676 9.49672 10.4993 10.5112 10.4993 11.7442C10.4993 12.9772 9.48322 13.9917 8.74376ZM15.7438 14.0052C14.7898 14.0052 14.0048 12.9847 14.0048 11.7442C14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.9743 17.3632C18.3038 19.1682 21.7463 19.7622 18.4342 23.5268 17.2427 23.8858 16.8027C23.7268 17.3632 19.6692 17.5623 19.7332 18.6733 19.6727C20.2043 19.1682 21.7463 18.4343 23.5268 17.0352 23.8858 16.8027C24.3653 12.4557 23.3048 8.62772 20.6438 5.10122 8.49734 4.78184ZM1.91512 7.80784C8.19912 5.04334 16.3311 4.45072 8.74376 19.1647 6.32126 4.43122 12.5208 4.45072C10.4103 4.51072 9.49672 10.0273 4.43122 11.0528 4.45072C10.4103 4.51072 10.0273 10.4103 10.4993 10.5112 14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.7438 9.48322 16.6978 9.48322 17.4828 10.5037 17.4828 11.7442C17.4828 12.9847 16.6978 13.9917 14.8503 13.9917ZM8.74376 13.9917C7.78076 13.9917 6.98826 12.9772 6.98826 11.7442C6.98826 10.5112 7.78076 9.49672 8.74376 9.49672C9.70676 9.49672 10.4993 10.5112 10.4993 11.7442C10.4993 12.9772 9.48322 13.9917 8.74376C13.6848 18.4347 14.8503 18.2792 15.9743 17.9942L8.99776 17.9962C10.1208 18.2797 11.2913 18.4342 12.5033 18.4572C13.6848 18.4347 14.8503 18.2792 15.9743 17.9942ZM8.74376 13.9917C7.78076 13.9917 6.98826 12.9772 6.98826 11.7442C6.98826 10.5112 7.78076 9.49672 8.74376 9.49672C9.70676 9.49672 10.4993 10.5112 10.4993 11.7442C10.4993 12.9772 9.48322 13.9917 8.74376ZM15.7438 14.0052C14.7898 14.0052 14.0048 12.9847 14.0048 11.7442C14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.9743 17.3632C18.3038 19.1682 21.7463 19.7622 18.4342 23.5268 17.2427 23.8858 16.8027C23.7268 17.3632 19.6692 17.5623 19.7332 18.6733 19.6727C20.2043 19.1682 21.7463 18.4343 23.5268 17.0352 23.8858 16.8027C24.3653 12.4557 23.3048 8.62772 20.6438 5.10122 8.49734 4.78184ZM1.91512 7.80784C8.19912 5.04334 16.3311 4.45072 8.74376 19.1647 6.32126 4.43122 12.5208 4.45072C10.4103 4.51072 9.49672 10.0273 4.43122 11.0528 4.45072C10.4103 4.51072 10.0273 10.4103 10.4993 10.5112 14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.7438 9.48322 16.6978 9.48322 17.4828 10.5037 17.4828 11.7442C17.4828 12.9847 16.6978 13.9917 14.8503 13.9917ZM8.74376 13.9917C7.78076 13.9917 6.98826 12.9772 6.98826 11.7442C6.98826 10.5112 7.78076 9.49672 8.74376 9.49672C9.70676 9.49672 10.4993 10.5112 10.4993 11.7442C10.4993 12.9772 9.48322 13.9917 8.74376C13.6848 18.4347 14.8503 18.2792 15.9743 17.9942L8.99776 17.9962C10.1208 18.2797 11.2913 18.4342 12.5033 18.4572C13.6848 18.4347 14.8503 18.2792 15.9743 17.9942ZM8.74376 13.9917C7.78076 13.9917 6.98826 12.9772 6.98826 11.7442C6.98826 10.5112 7.78076 9.49672 8.74376 9.49672C9.70676 9.49672 10.4993 10.5112 10.4993 11.7442C10.4993 12.9772 9.48322 13.9917 8.74376ZM15.7438 14.0052C14.7898 14.0052 14.0048 12.9847 14.0048 11.7442C14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.9743 17.3632C18.3038 19.1682 21.7463 19.7622 18.4342 23.5268 17.2427 23.8858 16.8027C23.7268 17.3632 19.6692 17.5623 19.7332 18.6733 19.6727C20.2043 19.1682 21.7463 18.4343 23.5268 17.0352 23.8858 16.8027C24.3653 12.4557 23.3048 8.62772 20.6438 5.10122 8.49734 4.78184ZM1.91512 7.80784C8.19912 5.04334 16.3311 4.45072 8.74376 19.1647 6.32126 4.43122 12.5208 4.45072C10.4103 4.51072 9.49672 10.0273 4.43122 11.0528 4.45072C10.4103 4.51072 10.0273 10.4103 10.4993 10.5112 14.0048 10.5037 14.7898 9.48322 14.8503 18.2792 15.7438 9.48322 16.6978 9.48322 17.4828 10.5037 17.4828 11.7442C17.4828 12.9847 16.6978 13.9917 14.8503 13.9917Z";

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