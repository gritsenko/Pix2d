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
                // Figma "Caption 9": 9px. The design data marks captions Bold Extended, but
                // Figma rasterizes that ≈ our regular weight, so Bold here would look heavier
                // than the mockup (see DesignAssets/figma_vs_app/ink_compare.png).
                new Style<Button>()
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(9)
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

                // Figma "Body 14": Zed Mono Extended 14px.
                new Style<Button>(s => s.Class("btn"))
                    .CornerRadius(10)
                    .FontSize(14)
                    .Margin(6)
                    .Height(36),

                new Style<ToggleButton>()
                    .FontSize(9)
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


                // Text classes mirroring the Figma type scale; captions and Body 11 sit on the
                // 60% tier. All weights stay regular: Figma's "Bold Extended" rasterizes like
                // our regular face, so real Bold would overshoot the mockup.
                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("caption"))
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(9)
                    .FontWeight(FontWeight.Normal)
                    .LineHeight(10)
                    .Foreground(StaticResources.Brushes.SecondaryForegroundBrush),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("body11"))
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(11)
                    .FontWeight(FontWeight.Normal)
                    .LineHeight(16)
                    .Foreground(StaticResources.Brushes.SecondaryForegroundBrush),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("body14"))
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(14)
                    .FontWeight(FontWeight.Normal)
                    .LineHeight(20)
                    .Foreground(StaticResources.Brushes.ForegroundBrush),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("body16"))
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                    .FontSize(16)
                    .FontWeight(FontWeight.Normal)
                    .LineHeight(24)
                    .Foreground(StaticResources.Brushes.ForegroundBrush),

                // An active toggle brightens its caption to the 90% tier.
                new Style<TextBlock>(s => s.OfType<ToggleButton>().Class(":checked").Descendant().OfType<TextBlock>().Class("caption"))
                    .Foreground(StaticResources.Brushes.ForegroundBrush),

                // AppButton / AppToggleButton glyph icons sit on the Figma icon tier: 70% white
                // when idle, full white once the toggle is checked (matches the toolbar icons).
                new Style<ContentControl>(s => s.OfType<ContentControl>().Name(AppButton.IconControlName))
                    .Foreground(StaticResources.Brushes.IconForegroundBrush),

                new Style<ContentControl>(s => s.OfType<ToggleButton>().Class(":checked").Descendant().OfType<ContentControl>().Name(AppButton.IconControlName))
                    .Foreground(Colors.White.ToBrush().ToImmutable()),


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

                // Compact (Narrow) overrides — declared after the base button styles above so they
                // win by order within this same style host. Halves the corner radius on app buttons
                // and toggle buttons everywhere in narrow mode (top bar, additional bar, action bar).
                new Style<Button>(_ => VisualStates.Narrow().OfType<Button>().Class("app-button"))
                    .CornerRadius(StaticResources.Measures.CompactButtonCornerRadius),
                new Style<ToggleButton>(_ => VisualStates.Narrow().OfType<ToggleButton>())
                    .CornerRadius(StaticResources.Measures.CompactButtonCornerRadius),
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