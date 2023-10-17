using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Pix2d.UI.Common.Extensions;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.Styles;

public partial class AppStyles : Avalonia.Styling.Styles
{
    public AppStyles()
    {
        AddRange(
            new Style[]
            {
                new Style<Border>(s => s.OfType<Border>().Class("Panel"))
                    .CornerRadius(new CornerRadius(0, 0))
                    .BorderBrush(StaticResources.Brushes.MainBackgroundBrush)
                    .BorderThickness(new Thickness()),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("FontIcon"))
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(16d)
                    .Foreground(Brushes.White)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<TextBlock>(s => s.OfType<TextBlock>().Class("Pix2dFontIcon"))
                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                    .FontSize(26d)
                    .Foreground(Brushes.White)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center),

                new Style<ToggleButton>(s => s.Is<ToggleButton>().Class(":checked").Template().Is<ContentPresenter>())
                    .Background(StaticResources.Brushes.SelectedItemBrush),
                
                new Style<ToggleButton>(s => s.Is<ToggleButton>().Class("secondary-button"))
                    .Foreground(Brushes.Gray),
                    
                new Style<ToggleButton>(s => s.Is<ToggleButton>().Class("secondary-button").Class(":checked").Template().Is<ContentPresenter>())
                    .Foreground(Brushes.White)
                    .Background(StaticResources.Brushes.SecondaryButtonBrush),
                
                new Style<ToggleButton>(s => s.Is<ToggleButton>().Class("secondary-button").Class(":pointerover"))
                    .Foreground(Brushes.White),
                
                new Style<ToggleButton>(s => s.Is<ToggleButton>().Class("secondary-button").Class(":pressed").Template().Is<ContentPresenter>())
                    .Foreground(Brushes.White),
                
                new Style<Button>(s => s.Is<Button>())
                    .Background(Brushes.Transparent)
                    .BorderThickness(new Thickness(0, 0)),
                
                new Style<Button>(s => s.Is<Button>().Class("brush-button"))
                    .Background(StaticResources.Brushes.BrushButtonBrush),
                
                new Style<Button>(s => s.Is<Button>().Class(":pointerover").Not(b => b.Class("color-button")).Template().Is<ContentPresenter>())
                    .Background(StaticResources.Brushes.ButtonHoverBrush),
                
                new Style<Button>(s => s.Is<Button>().Class(":pointerover").Class("color-button").Template().Is<ContentPresenter>())
                    .BorderBrush(StaticResources.Brushes.ButtonHoverBrush),
                
                new Style<Button>(s => s.Is<Button>().Class("secondary-button"))
                    .Background(StaticResources.Brushes.SecondaryButtonBrush),
                
                new Style<Button>(s => s.Is<Button>().Class(":pressed").Not(b => b.Class("color-button")).Template().Is<ContentPresenter>())
                    .Background(StaticResources.Brushes.ButtonActiveBrush),
                
                new Style<TextBlock>(x=>x.OfType<TextBlock>().Class("ToolIcon"))
                    .FontSize(26),
                
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
            }
        );

        AddRange(AdaptiveLayout);
        
        Resources["ThemeAccentColor"] = StaticResources.Colors.MyAccentColor;
        Resources["ThemeAccentBrush"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush2"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush3"] = StaticResources.Brushes.AccentBrush;
        Resources["ThemeAccentBrush4"] = StaticResources.Brushes.AccentBrush;

        //border and slider
        Resources["ThemeBorderLowBrush"] = Brushes.Gray;
    }
}