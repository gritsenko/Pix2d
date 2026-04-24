using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.Styles;

public partial class AppStyles : Avalonia.Styling.Styles
{
    public AppStyles()
    {
        AddRange([
                new Style<Button>()
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(8)
                    .BorderThickness(new Thickness(0, 0)),

                new Style<Button>(s => s.OfType<Button>().Not(x => x.Class(":pointerover")).Not(x => x.Class(":pressed")))
                    .Background(Brushes.Transparent),

                new Style<ToggleButton>(s => s.OfType<ToggleButton>().Not(x => x.Class(":pointerover")).Not(x => x.Class(":pressed")))
                    .Background(Brushes.Transparent),

                new Style<Button>(s => s.Class("small-button"))
                    .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
                    .Width(StaticResources.Measures.SmallButtonSize)
                    .Height(StaticResources.Measures.SmallButtonSize)
                    .Margin(4),

                new Style<Button>(s => s.Class("app-button"))
                    .CornerRadius(12)
                    .Margin(6)
                    .Width(44)
                    .Height(44),

                new Style<Button>(s => s.Class("btn"))
                    .CornerRadius(10)
                    .FontSize(14)
                    .Margin(6)
                    .Height(36),

                new Style<ToggleButton>()
                    .FontSize(8)
                    .Margin(6)
                    .Width(44)
                    .Height(44)
                    .BorderThickness(0)
                    .CornerRadius(12),

                new Style<Button>(s => s.Class("btn-bright"))
                    .Background(Colors.White.WithAlpha(0.1f).ToBrush().ToImmutable()),

                new Style<AppButton>()
                    .Margin(6)
                    .Width(44)
                    .Height(44),


                new Style<TextBlock>(s => s.Is<AppButton>().Class("app-button").Descendant().OfType<TextBlock>())
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily),


                new Style<Border>(s => s.Class("Panel"))
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
                    .BorderThickness(1)
                    .CornerRadius(new CornerRadius(StaticResources.Measures.PanelCornerRadius))
                    .ClipToBounds(true),

                new Style<TextBlock>(s => s.Class("FontIcon"))
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(10d)
                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<TextBlock>(s => s.Class("Pix2dFontIcon"))
                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                    .FontSize(24d)
                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<Button>(s => s.OfType<ProjectItem>().Child())
                    .BorderThickness(new Thickness(2))
                    .BorderBrush(Brushes.Transparent),
                new Style<Button>(s => s.OfType<ProjectItem>().Child().Class(":pointerover"))
                    .BorderThickness(new Thickness(2))
                    .BorderBrush(StaticResources.Brushes.ButtonActiveBrush),

                new Style<ComboBox>()
                    .Height(32)
                    .CornerRadius(10)
                    .Background(StaticResources.Brushes.BrushButtonBrush)
                    .BorderThickness(0),
            ]
        );

        Resources["ThemeAccentColor"] = StaticResources.Colors.MyAccentColor;
        Resources["ThemeAccentBrush"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush2"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush3"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush4"] = StaticResources.Brushes.AccentBrush;

        //border and slider
        Resources["ThemeBorderLowBrush"] = Brushes.Gray;

        //button
        Resources["ThemeControlHighBrush"] = StaticResources.Brushes.SelectedToggleButtonBrush;
        Resources["ThemeControlMidBrush"] = StaticResources.Brushes.ButtonHoverBrush;
        Resources["ThemeControlLowBrush"] = StaticResources.Brushes.ButtonHoverBrush;
        //Resources["ThemeBorderMidBrush"] = Brushes.GreenYellow;
    }
}