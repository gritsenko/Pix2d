using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Command;
using Pix2d.UI.Resources;
using Pix2d.UI.Styles;

namespace Pix2d.UI.MainMenu;

public partial class MainMenuView : ViewBase<MainMenuView.State>
{
    public const string BackButtonName = "back-button";

    public MainMenuView(AppState appState, ICommandService commandService, IServiceProvider serviceProvider)
        : base(new State(appState, commandService, serviceProvider))
    {
    }

    protected override StyleGroup BuildStyles() =>
    [
        //Typed style definition
        new Style<Button>(s => s.OfType<Button>().Class("selected"))
            .Background(StaticResources.Brushes.AccentBrush),

        //General style definition
        new Style(s => s.OfType<Button>().Class("selected"))
            .Setter(TemplatedControl.BackgroundProperty, StaticResources.Brushes.AccentBrush),

        new Style<Grid>(s => s.Name("main-menu-content")).Col(1),

        new Style<MainMenuItemView>(s => s.Name(BackButtonName))
            .IsVisible(false),

        new Style<Button>(s => s.OfType<MainMenuItemView>().Class(MainMenuItemView.SelectedClass).Child())
            .Background(StaticResources.Brushes.ButtonHoverBrush),



        new StyleGroup(s=>VisualStates.Narrow())
        {
            new Style<Grid>(s => s.Name("main-menu-content"))
                .Col(0)
                .ColSpan(2),

            new Style<MainMenuItemView>(s => s.Name(BackButtonName))
                .IsVisible(true),

            new Style<ItemsControl>(s => s.Name("main-menu-buttons"))
                .Col(0)
                .ColSpan(2),

        }
    ];

    protected override object Build(State state) =>
        new Border()
            //.DisableBlur(false)
            .Child(
                new Grid()
                    .Cols("200,*")
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .Children(
                        new ItemsControl()
                            .Name("main-menu-buttons")
                            .Items(state.MenuItems),
                        new Grid().Rows("auto,*")
                            .IsVisible(state, x => x.ShowMenuContent)
                            .Name("main-menu-content")
                            .Children(
                                new MainMenuItemView()
                                    .Name(BackButtonName)
                                    .Header(L("Back"))
                                    .Icon("\xEC52")
                                    .OnClicked(_ => state.Back()),
                                new ScrollViewer().Row(1)
                                    .Background(StaticResources.Brushes.MainMenuBackgroundBrush)
                                    .Content(state, x => x.MenuContent!)
                            )
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IServiceProvider _serviceProvider;
        private readonly ViewCommands _viewCommands;
        private readonly FileCommands _fileCommands;

        [ObservableProperty]
        public partial bool ShowMenuContent { get; set; } = true;

        [ObservableProperty]
        public partial MainMenuItemView? SelectedItem { get; set; }

        [ObservableProperty]
        public partial Control? MenuContent { get; set; }

        public State(AppState appState, ICommandService commandService, IServiceProvider serviceProvider)
        {
            _appState = appState;
            _serviceProvider = serviceProvider;
            _viewCommands = commandService.GetCommandList<ViewCommands>()!;
            _fileCommands = commandService.GetCommandList<FileCommands>()!;

            MenuItems = CreateMenuItems();

            _appState.UiState.WatchFor(x => x.ShowMenu, OnMenuVisibilityChanged);
            if (SKInput.Current != null)
                SKInput.Current.KeyPressed += OnKeyPressed;

            OnMenuVisibilityChanged();
        }

        public MainMenuItemView[] MenuItems { get; }

        public void Back()
        {
            ShowMenuContent = false;
            SelectMenuItem(null);
        }

        public void SelectMenuItem(MainMenuItemView? selectedItem)
        {
            var lastItem = SelectedItem;
            SelectedItem = selectedItem;

            if (lastItem == SelectedItem)
                return;

            foreach (var item in MenuItems)
                item.IsSelected = item == selectedItem;

            MenuContent = selectedItem?.ContentViewType != null
                ? ActivatorUtilities.CreateInstance(_serviceProvider, selectedItem.ContentViewType) as Control
                : null;

            if (SelectedItem != null)
                ShowMenuContent = true;
        }

        private void OnKeyPressed(object? sender, KeyboardActionEventArgs e)
        {
            if (_appState.UiState.ShowMenu && e.Key == VirtualKeys.Escape)
                Close();
        }

        private void Close()
        {
            _viewCommands.HideMainMenuCommand.Execute();
        }

        private void Save()
        {
            Close();
            _fileCommands.Save.Execute();
        }

        private void OnMenuVisibilityChanged()
        {
            ShowMenuContent = _appState.UiState.VisualState == nameof(VisualStates.Wide);
            SelectMenuItem(
                ShowMenuContent
                    ? MenuItems.FirstOrDefault(x => x.ContentViewType == typeof(InfoView))
                    : null);
        }

        private MainMenuItemView[] CreateMenuItems() =>
        [
            new MainMenuItemView()
                .Header(L("Back"))
                .Icon("\xEC52")
                .OnClicked(_ => Close()),
            new MainMenuItemView()
                .Header(L("Info"))
                .Icon("\xEADF")
                .ContentViewType(typeof(InfoView))
                .OnClicked(SelectMenuItem),
            new MainMenuItemView()
                .Header(L("Commands"))
                .Icon("\xE71D")
                .ContentViewType(typeof(CommandsView))
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
                .OnClicked(SelectMenuItem)
        ];
    }
}