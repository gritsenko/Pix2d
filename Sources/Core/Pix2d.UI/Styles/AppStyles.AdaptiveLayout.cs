using Avalonia.Styling;
using Pix2d.UI;
using Pix2d.UI.Export;
using Pix2d.UI.Layers;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.ToolBar;

namespace Pix2d.Styles;

public partial class AppStyles
{
    private static readonly Style[] AdaptiveLayout = {
        
        // Main layout
        new(s => s.WideScreen().ToolbarContainer())
        {
            Setters =
            {
                new Setter(Grid.ColumnProperty, 0),
                new Setter(Grid.RowProperty, 2),
                new Setter(Grid.RowSpanProperty, 2),
            }
        },
        
        // Toolbar
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
                new Setter(Grid.RowProperty, 4),
                new Setter(Grid.RowSpanProperty, 1),
                new Setter(Grid.ColumnSpanProperty, 3),
                new Setter(Border.BackgroundProperty, StaticResources.Brushes.PanelsBackgroundBrush),
                new Setter(Layoutable.MarginProperty, new Thickness(0, 1, 0, 0)),
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
        },
        
        // TOP BAR
        
        new Style<AppButton>(s => s.WideScreen().TopBarButton())
            .Width(51)
            .Height(51),
        new Style<AppButton>(s => s.NarrowScreen().TopBarButton())
            .Width(40)
            .Height(40),
        
        new(s => s.NarrowScreen().TopBarButton().ButtonIcon())
        {
            Setters = {
                new Setter(Grid.RowSpanProperty, 2),
            }
        },
        new Style<TextBlock>(s => s.NarrowScreen().TopBarButton().ButtonLabel()).IsVisible(false),
        
        new Style<LayersView>(s => s.OfType<LayersView>()).VerticalAlignment(VerticalAlignment.Top),
        new Style<LayersView>(s => s.NarrowScreen().Descendant().OfType<LayersView>()).VerticalAlignment(VerticalAlignment.Bottom),
        
        // MAIN MENU
        
        new(s => s.WideScreen().MainMenuContent())
        {
            Setters =
            {
                new Setter(Grid.ColumnProperty, 1),
            }
        },
        new(s => s.NarrowScreen().MainMenuContent())
        {
            Setters =
            {
                new Setter(Grid.ColumnSpanProperty, 2),
            }
        },
        new(s => s.NarrowScreen().MainMenuButtons())
        {
            Setters =
            {
                new Setter(Grid.ColumnSpanProperty, 2),
            }
        },
        new Style<ItemsControl>(s => s.NarrowScreen().MenuItemSelected().MainMenuButtons()).IsVisible(false),
        new Style<Border>(s => s.NarrowScreen().MenuItemNotSelected().MainMenuContent()).IsVisible(false),
        
        new Style<Button>(s => s.WideScreen().BackButton()).IsVisible(false),
        new Style<Button>(s => s.NarrowScreen().BackButton()).IsVisible(true),
        
        new Style<Button>(s => s.WideScreen().Descendant().MenuItem().Class(MainMenuItemView.SelectedClass).Child()).Background(StaticResources.Brushes.ButtonHoverBrush),
        new Style<Button>(s => s.NarrowScreen().MenuItemSelected().Descendant().MenuItem().Class(MainMenuItemView.SelectedClass).Child()).Background(StaticResources.Brushes.ButtonHoverBrush),
        
        // ADDITIONAL TOP BAR
        
        new Style<AdditionalTopBarView>(s => s.WideScreen().Descendant().OfType<AdditionalTopBarView>())
            .VerticalAlignment(VerticalAlignment.Top)
            .HorizontalAlignment(HorizontalAlignment.Right),
        new Style<AdditionalTopBarView>(s => s.NarrowScreen().Descendant().OfType<AdditionalTopBarView>())
            .VerticalAlignment(VerticalAlignment.Bottom)
            .HorizontalAlignment(HorizontalAlignment.Right),
        
        // ACTIONS BAR
        
        new Style<ActionsBarView>(s => s.WideScreen().Descendant().OfType<ActionsBarView>())
            .Margin(new Thickness(0, 33, 0, 0)),
        new Style<ActionsBarView>(s => s.NarrowScreen().Descendant().OfType<ActionsBarView>())
            .Margin(new Thickness(3, 0)),
        new Style<Button>(s => s.WideScreen().ActionsBarButton())
            .Width(58)
            .Height(58),
        new Style<Button>(s => s.NarrowScreen().ActionsBarButton())
            .Width(48)
            .Height(48),
        new Style<TextBlock>(s => s.NarrowScreen().ActionsBarButton().ButtonLabel())
            .FontSize(9),
        
        
        // EXPORT
        
        new(s => s.WideScreen().Descendant().Name(ExportView.PreviewName))
        {
            Setters = {
                new Setter(Grid.ColumnProperty, 0),
                new Setter(Grid.ColumnSpanProperty, 1),
                new Setter(Grid.RowProperty, 0),
                new Setter(Grid.RowSpanProperty, 2),
            }
        },
        new(s => s.WideScreen().Descendant().Name(ExportView.SettingsName))
        {
            Setters = {
                new Setter(Grid.ColumnProperty, 1),
                new Setter(Grid.ColumnSpanProperty, 1),
                new Setter(Grid.RowProperty, 0),
                new Setter(Grid.RowSpanProperty, 2),
                new Setter(Layoutable.MarginProperty, new Thickness(16, 0))
            }
        },
        new(s => s.NarrowScreen().Descendant().Name(ExportView.PreviewName))
        {
            Setters = {
                new Setter(Grid.ColumnProperty, 0),
                new Setter(Grid.ColumnSpanProperty, 2),
                new Setter(Grid.RowProperty, 0),
                new Setter(Grid.RowSpanProperty, 1),
            }
        },
        new(s => s.NarrowScreen().Descendant().Name(ExportView.SettingsName))
        {
            Setters = {
                new Setter(Grid.ColumnProperty, 0),
                new Setter(Grid.ColumnSpanProperty, 2),
                new Setter(Grid.RowProperty, 1),
                new Setter(Grid.RowSpanProperty, 1),
                new Setter(Layoutable.MarginProperty, new Thickness(0, 16))
            }
        },
        new(s => s.NarrowScreen().Descendant().OfType<Pix2d.UI.Export.ExportProWarningView>())
        {
            Setters = {
                new Setter(Grid.ColumnSpanProperty, 2),
            }
        },
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
    public static Selector TopBarButton(this Selector s) => s.Descendant().Class("TopBar");
    public static Selector ActionsBarButton(this Selector s) => s.Descendant().Class(ActionsBarView.ButtonClass);
    public static Selector ButtonIcon(this Selector s) => s.Descendant().Name(AppButton.IconControlName);
    public static Selector ButtonLabel(this Selector s) => s.Descendant().Name(AppButton.LabelControlName);
    
    // Main Menu
    public static Selector MainMenu(this Selector s) => s.Descendant().OfType<MainMenuView>();
    public static Selector MainMenuButtons(this Selector s) => s.Descendant().Name(MainMenuView.MainMenuButtonsName);
    public static Selector MainMenuContent(this Selector s) => s.Descendant().Name(MainMenuView.MainMenuContentName);
    public static Selector MenuItemSelected(this Selector s) => s.MainMenu().Class(MainMenuView.ItemSelectedClass);
    public static Selector MenuItemNotSelected(this Selector s) => s.MainMenu().Not(ss => ss.Class(MainMenuView.ItemSelectedClass));
    public static Selector BackButton(this Selector s) => s.MainMenu().Descendant().Name(MainMenuView.BackButtonName);
    public static Selector MenuItem(this Selector s) => s.OfType<MainMenuItemView>();
}
