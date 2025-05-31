using Avalonia.Styling;
using Avalonia.Xaml.Interactions.Custom;
using Pix2d.Command;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using SkiaNodes.Interactive;

namespace Pix2d.UI.MainMenu;

public class MainMenuView : LocalizedComponentBase
{
    public const string BackButtonName = "back-button";

    protected override StyleGroup BuildStyles() =>
    [
        //Typed style definition
        new Style<Button>(s => s.OfType<Button>().Class("selected"))
            .Background(StaticResources.Brushes.AccentBrush),

        //General style definition
        new Style(s => s.OfType<Button>().Class("selected"))
            .Setter(TemplatedControl.BackgroundProperty, StaticResources.Brushes.AccentBrush),

        new Style<Grid>(s => s.Name("main-menu-content")).Col(1),
        new Style<Button>(s => s.Name(BackButtonName)).IsVisible(false),
        new Style<Button>(s => s.OfType<MainMenuItemView>().Class(MainMenuItemView.SelectedClass).Child())
            .Background(StaticResources.Brushes.ButtonHoverBrush),



        new StyleGroup(s=>VisualStates.Narrow())
        {
            new Style<Grid>(s => s.Name("main-menu-content"))
                .Col(0)
                .ColSpan(2),

            new Style<Button>(s => s.Name(BackButtonName)).IsVisible(true),

            new Style<ItemsControl>(s => s.Name("main-menu-buttons"))
                .Col(0)
                .ColSpan(2),

        }
    ];

    protected override object Build()
    {
        _menuItems =
        [
            new MainMenuItemView()
                .Header(L("Back"))
                .Icon("")
                .OnClicked(_ => Close()),
            new MainMenuItemView()
                .Header(L("Info"))
                .Icon("\xEADF")
                .ContentViewType(typeof(InfoView))
                .OnClicked(SelectMenuItem),
            new MainMenuItemView()
                .Header(L("New"))
                .Icon("\xE7C3")
                .ContentViewType(typeof(NewDocumentView))
                .OnClicked(SelectMenuItem),
            new MainMenuItemView()
                .Header(L("Open"))
                .Icon("\xED41")
                .ContentViewType(typeof(OpenDocumentView))
                .OnClicked(SelectMenuItem),
            new MainMenuItemView()
                .Header(L("Save"))
                .Icon("\xE74E")
                .OnClicked(_ => Save()),
            new MainMenuItemView()
                .Header(L("Save as"))
                .Icon("\xE792")
                .ContentViewType(typeof(SaveDocumentView))
                .OnClicked(SelectMenuItem),
            //new MainMenuItemView()
            //    .Header(L("License"))
            //    .Icon("\xE719")
            //    .ContentViewType(typeof(LicenseView))
            //    .OnClicked(SelectMenuItem)
        ];

        return new BlurPanel()
            .Child(
                new Grid()
                    .Cols("200,*")
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .Children(
                        new ItemsControl()
                            .Name("main-menu-buttons")
                            .Items(_menuItems),
                        new Grid().Rows("auto,*")
                            .IsVisible(() => ShowMenuContent)
                            .Name("main-menu-content")
                            .Children(
                                new MainMenuItemView()
                                    .Name(BackButtonName)
                                    .Header("Back")
                                    .Icon("")
                                    .OnClicked(_ => Back()),
                                new ScrollViewer().Row(1)
                                    .Background(StaticResources.Brushes.MainMenuBackgroundBrush)
                                    .Ref(out _menuContentScrollViewer)
                            )
                    )
            );
    }

    private ScrollViewer _menuContentScrollViewer;

    private MainMenuItemView[] _menuItems;

    [Inject] private AppState AppState { get; set; }
    [Inject] private ICommandService CommandService { get; set; }

    public bool ShowMenuContent { get; set; } = true;
    public MainMenuItemView? SelectedItem { get; set; }

    protected override void OnAfterInitialized()
    {
        // Reset selected menu item to the default when menu is closed
        AppState.UiState.WatchFor(x => x.ShowMenu, () =>
        {
            ShowMenuContent = AppState.UiState.VisualState == nameof(VisualStates.Wide);
            SelectMenuItem(
                ShowMenuContent
                    ? _menuItems.FirstOrDefault(x => x.ContentViewType == typeof(InfoView))
                    : null);
            StateHasChanged();
        });

        SKInput.Current.KeyPressed += OnKeyPressed;
    }
    private void OnKeyPressed(object? sender, KeyboardActionEventArgs e)
    {
        if (AppState.UiState.ShowMenu && e.Key == VirtualKeys.Escape) 
            Close();
    }

    protected override void OnLocaleChanged()
    {
        base.OnLocaleChanged();

        foreach (var mainMenuItemView in _menuItems)
        {
            mainMenuItemView.UpdateState();
        }
    }

    private void SelectMenuItem(MainMenuItemView? selectedItem)
    {
        var lastItem = SelectedItem;
        SelectedItem = selectedItem;

        if (lastItem != SelectedItem)
        {
            foreach (var item in _menuItems)
                item.IsSelected = item == selectedItem;

            if (selectedItem?.ContentViewType != null)
            {
                _menuContentScrollViewer.Content = Activator.CreateInstance(selectedItem.ContentViewType);
            }

            if (SelectedItem != null)
                ShowMenuContent = true;
            StateHasChanged();
        }
    }

    private void Close()
    {
        CommandService.GetCommandList<ViewCommands>()?.HideMainMenuCommand.Execute();
    }

    private void Back()
    {
        ShowMenuContent = false;
        SelectMenuItem(null);
    }

    private void Save()
    {
        Close();
        CommandService.GetCommandList<FileCommands>()?.Save.Execute();
    }
}