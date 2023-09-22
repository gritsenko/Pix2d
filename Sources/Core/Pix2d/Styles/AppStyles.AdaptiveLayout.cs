using Avalonia.Styling;
using Pix2d.Views;
using Pix2d.Views.ToolBar;

namespace Pix2d.Styles;

public partial class AppStyles
{
    private static readonly Style[] AdaptiveLayout = {
        new(s => s.WideScreen().ToolbarContainer())
        {
            Setters =
            {
                new Setter(Grid.ColumnProperty, 0),
                new Setter(Grid.RowProperty, 2),
            }
        },
        new Style<Button>(s => s.WideScreen().Toolbar().Descendant().OfType<Button>().Class("toolbar-button"))
            .Width(51).Height(51).Padding(new Thickness(0)),
        new Style<Button>(s => s.WideScreen().Toolbar().Descendant().OfType<Button>().Class("brush-button"))
            .Margin(new Thickness(0, 8)),
        new Style<Button>(s => s.WideScreen().Toolbar().Descendant().OfType<Button>().Class("color-button"))
            .Width(40).Height(40).Margin(new Thickness(0, 8)),
        new Style<Button>(s => s.NarrowScreen().Toolbar().Descendant().OfType<Button>().Class("toolbar-button"))
            .Width(40).Height(40).Padding(new Thickness(0)).VerticalAlignment(VerticalAlignment.Top),
        new Style<Button>(s => s.NarrowScreen().Toolbar().Descendant().OfType<Button>().Class("color-button"))
            .Width(26).Height(26).Margin(new Thickness(8, 6, 8, 6)).VerticalAlignment(VerticalAlignment.Top),
        
        new Style<Border>(s => s.WideScreen().Toolbar().Descendant().OfType<ToolItemView>().Descendant().OfType<Border>())
            .BorderThickness(new Thickness(4, 0, 0, 0)),
        new Style<Border>(s => s.NarrowScreen().Toolbar().Descendant().OfType<ToolItemView>().Descendant().OfType<Border>())
            .BorderThickness(new Thickness(0, 0, 0, 4)),
        
        new Style<Grid>(s => s.NarrowScreen().Descendant().Class("toolbar-button-container"))
            .Height(40).Width(40).VerticalAlignment(VerticalAlignment.Top),
        
        new Style<TextBlock>(x=>x.Class("ToolIcon"))
            .Height(26)
            .Width(26)
            .TextAlignment(TextAlignment.Center)
            .FontSize(26),
        new Style<TextBlock>(x=>x.NarrowScreen().Descendant().Class("ToolIcon"))
            .Height(22)
            .Width(22)
            .TextAlignment(TextAlignment.Center)
            .FontSize(22),
        
        new Style<Border>(s => s.NarrowScreen().ToolbarContainer())
        {
            Setters =
            {
                new Setter(Grid.ColumnProperty, 0),
                new Setter(Grid.RowProperty, 3),
                new Setter(Grid.ColumnSpanProperty, 3),
                new Setter(Layoutable.HeightProperty, 44.0),
                new Setter(Border.BackgroundProperty, StaticResources.Brushes.PanelsBackgroundBrush)
            }
        },
        new(s => s.NarrowScreen().ToolbarContainer().Child().OfType<ScrollViewer>())
        {
            Setters =
            {
                new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled),
                new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden),
            }
        },
        new(s => s.NarrowScreen().Toolbar().Child().OfType<StackPanel>())
        {
            Setters = {
                new Setter(StackPanel.OrientationProperty, Orientation.Horizontal),
            }
        },
        new(s => s.NarrowScreen().InfoPanel())
        {
            Setters =
            {
                new Setter(Visual.IsVisibleProperty, false),
            }
        }
    };

}
public static class StyleSelectors
{
    public const string WideClass = "wide";
    public const string NarrowClass = "small";

    public static Selector WideScreen(this Selector s) => s.Class(WideClass);
    public static Selector NarrowScreen(this Selector s) => s.Class(NarrowClass);
    public static Selector ToolbarContainer(this Selector s) => s.Descendant().Name("toolbar");
    public static Selector Toolbar(this Selector s) => s.Descendant().OfType<ToolBarView>();
    public static Selector InfoPanel(this Selector s) => s.Descendant().OfType<InfoPanelView>();
    public static Selector ToolbarButton(this Selector s) => s.Descendant().OfType<Button>().Class("toolbar-button");
}
