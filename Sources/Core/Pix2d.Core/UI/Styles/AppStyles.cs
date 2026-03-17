using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Pix2d.Common.Extensions;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.Styles;

public partial class AppStyles : Avalonia.Styling.Styles
{
    public AppStyles()
    {
        AddRange(
            new Style[]
            {
                new Style<Button>(s => s.Is<Button>().Class("SmallButton"))
                    .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
                    .Width(StaticResources.Measures.SmallButtonSize)
                    .Height(StaticResources.Measures.SmallButtonSize)
                    .Margin(4),

                new Style<Button>(s => s.Is<Button>().Class("app-button"))
                    .CornerRadius(12)
                    .Margin(6)
                    .Width(44)
                    .Height(44),

                new Style<Button>(s => s.Is<Button>().Class("btn"))
                    .CornerRadius(10)
                    .FontSize(14)
                    .Margin(6)
                    .Height(36),

                new Style<Button>(s => s.Is<Button>().Class("btn-bright"))
                    .Background(Colors.White.WithAlpha(0.1f).ToBrush().ToImmutable()),

                new Style<AppButton>(s => s.Is<AppButton>())
                    .Margin(6)
                    .Width(44)
                    .Height(44),


                new Style<TextBlock>(s => s.Is<AppButton>().Class("app-button").Descendant().OfType<TextBlock>())
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily),


                new Style<Border>(s => s.OfType<Border>().Class("Panel"))
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
                    .BorderThickness(1)
                    .CornerRadius(new CornerRadius(StaticResources.Measures.PanelCornerRadius))
                    .ClipToBounds(true),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("FontIcon"))
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(10d)
                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("Pix2dFontIcon"))
                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                    .FontSize(24d)
                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<ToggleButton>(s => s.Is<ToggleButton>())
                    .CornerRadius(12),

                new Style<ToggleButton>(s => s.Is<ToggleButton>().Class(":checked").Template().Is<ContentPresenter>())
                    .Background(StaticResources.Brushes.SelectedToggleButtonBrush),
                
                new Style<Button>(s => s.Is<Button>())
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(8)
                    .Background(Brushes.Transparent)
                    .BorderThickness(new Thickness(0, 0)),
                
                new Style<Button>(s => s.Is<Button>().Class(":pointerover").Not(b => b.Class("color-button")).Template().Is<ContentPresenter>())
                    .Background(StaticResources.Brushes.ButtonHoverBrush),
                
                new Style<Button>(s => s.Is<Button>().Class(":pointerover").Class("color-button").Template().Is<ContentPresenter>())
                    .BorderBrush(StaticResources.Brushes.ButtonHoverBrush),
                
                new Style<Button>(s => s.Is<Button>().Class(":pressed").Not(b => b.Class("color-button")).Template().Is<ContentPresenter>())
                    .Background(StaticResources.Brushes.ButtonActiveBrush),
                
                new Style<ToggleSwitch>(s => s.OfType<ToggleSwitch>().Class(":pointerover").Template().Is<ContentPresenter>())
                    .Background(SKColor.Empty.ToBrush()),

                new Style<ToggleSwitch>(s => s.OfType<ToggleSwitch>().Class(":checked").Template().Is<ContentPresenter>())
                    .Background(SKColor.Empty.ToBrush()),
                
                new Style<Button>(s => s.OfType<ProjectItem>().Child())
                    .BorderThickness(new Thickness(2))
                    .BorderBrush(Brushes.Transparent),
                new Style<Button>(s => s.OfType<ProjectItem>().Child().Class(":pointerover"))
                    .BorderThickness(new Thickness(2))
                    .BorderBrush(StaticResources.Brushes.ButtonActiveBrush),

                new Style<ComboBox>(s=>s.OfType<ComboBox>())
                    .Height(32)
                    .CornerRadius(10)
                    .Background(StaticResources.Brushes.BrushButtonBrush)
                    .BorderThickness(0),
            }
        );

        Resources["ThemeAccentColor"] = StaticResources.Colors.MyAccentColor;
        Resources["ThemeAccentBrush"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush2"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush3"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush4"] = StaticResources.Brushes.AccentBrush;

        //border and slider
        Resources["ThemeBorderLowBrush"] = Brushes.Gray;

        //button
        //Resources["ThemeControlHighBrush"] = Brushes.GreenYellow;
        //Resources["ThemeBorderMidBrush"] = Brushes.GreenYellow;
    }
}