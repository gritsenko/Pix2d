using Avalonia.Styling;
using Pix2d.Shared;

namespace Pix2d;

public class AppStyles : Styles
{
    public AppStyles()
    {
        AddRange(
            new Style[]
            {
                new Style<Border>(s => s.Class("Panel"))
                    .CornerRadius(new CornerRadius(0, 0))
                    .Background("#444E59".ToColor().ToBrush())
                    .BorderBrush(StaticResources.Brushes.MainBackgroundBrush)
                    .BorderThickness(new Thickness()),

                new Style<TextBlock>(s => s.Class("FontIcon"))
                    //.FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(16d)
                    .Foreground(Brushes.White)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<TextBlock>(s => s.Class("Pix2dFontIcon"))
                    //.FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                    .FontSize(26d)
                    .Foreground(Brushes.White)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<AppButton>(s => s.Class("TopBar"))
                    .Width(52d)
                    .Height(52d),

                new Style<Button>()
                    .Background(Brushes.Transparent)
                    .BorderThickness(new Thickness(0, 0)),

                new Style<ToggleButton>()
                    .Background(Brushes.Transparent)
                    .BorderThickness(new Thickness(0, 0)),

                new Style<ToggleButton>(s => s.Class(":pointerover"))
                    .Background(StaticResources.Brushes.ButtonHoverBrush),

                new Style<Button>(s => s.Class(":pointerover"))
                    .Background(StaticResources.Brushes.ButtonHoverBrush),

                new Style<AppButton>(s => s.Class(":pointerover"))
                    .Setter(AppButton.BackgroundProperty, StaticResources.Brushes.ButtonHoverBrush),

                new Style<AppToggleButton>(s => s.Class(":pointerover"))
                    .Setter(AppToggleButton.BackgroundProperty, StaticResources.Brushes.ButtonHoverBrush),

            }
        );

        Resources["ThemeAccentColor"] = StaticResources.Colors.MyAccentColor;
        Resources["ThemeAccentBrush"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush2"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush3"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush4"] = StaticResources.Brushes.AccentBrush;

        //border and slider
        Resources["ThemeBorderLowBrush"] = Brushes.Gray;
    }
}