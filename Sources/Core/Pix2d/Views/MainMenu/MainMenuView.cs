using Avalonia.Styling;
using SkiaNodes.Interactive;

namespace Pix2d.Views.MainMenu;

public class MainMenuView : ComponentBase
{
    public const string MainMenuViewName = "main-menu";
    public const string MainMenuButtonsName = "main-menu-buttons";
    public const string MainMenuContentName = "main-menu-content";
    public const string ItemSelectedClass = "item-selected";
    public const string BackButtonName = "back-button";
    private const string DefaultItem = "Info";
    
    private MainMenuItemView[] _menuItems;

    public MainMenuView(UiState state)
    {
        // Reset selected menu item to the default when menu is closed
        state.WatchFor(x => x.ShowMenu, () => SelectItem(null));
        SKInput.Current.KeyPressed += OnKeyPressed;
    }

    private void OnKeyPressed(object sender, KeyboardActionEventArgs e)
    {
        if (e.Key == VirtualKeys.Escape && e.Modifiers == KeyModifier.None)
        {
            Close();
        }
    }

    protected override object Build()
    {
        this.Styles(
            //Typed style definition
            new Style<Button>(s => s.OfType<Button>().Class("selected"))
                .Background(StaticResources.Brushes.AccentBrush),

            //General style definition
            new Style(s => s.OfType<Button>().Class("selected"))
                .Setter(TemplatedControl.BackgroundProperty, StaticResources.Brushes.AccentBrush)
        );

        _menuItems = new[] {
            new MainMenuItemView()
                .Header("Back")
                .Icon("")
                .OnClicked(_ => Close()),
            new MainMenuItemView()
                .Header("Info")
                .Icon("\xEADF")
                .OnClicked(OnItemClick),
            new MainMenuItemView()
                .Header("New")
                .Icon("\xE7C3")
                .OnClicked(OnItemClick),
            new MainMenuItemView()
                .Header("Open")
                .Icon("\xED41")
                .OnClicked(OnItemClick),
            new MainMenuItemView()
                .Header("Save")
                .Icon("\xE74E")
                .OnClicked(_ => Save()),
            new MainMenuItemView()
                .Header("Save as")
                .Icon("\xE792")
                .OnClicked(OnItemClick)
        };

        return new Border()
            .Background(Brushes.DarkGray)
            .Child(
                new Grid()
                    .Cols("200,*")
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .Name(MainMenuViewName)
                    .Children(
                        new ItemsControl()
                            .Name(MainMenuButtonsName)
                            .Items(_menuItems),
                        new Grid().Rows("auto,*")
                            .Name(MainMenuContentName)
                            .Children(
                                new MainMenuItemView()
                                    .Name(BackButtonName)
                                    .Header("Back")
                                    .Icon("")
                                    .OnClicked(_ => Back()),
                                new Border().Row(1)
                                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                                    .Padding(new Thickness(0, 8))
                                    .Child(
                                        new StackPanel().Children(
                                            new InfoView().IsVisible(() => SelectedItem == "Info"),
                                            new NewDocumentView().IsVisible(() => SelectedItem == "New"),
                                            new OpenDocumentView().IsVisible(() => SelectedItem == "Open"),
                                            new SaveDocumentView().IsVisible(() => SelectedItem == "Save as")
                                        )
                                        // new ContentControl()
                                        //     .Ref(out _tabContent)
                                    )
                            )
                    )
            );
    }

    private bool _isActivelySelected;
    private string _selectedItem;

    public string SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            StateHasChanged();
        }
    }

    // True if the menu item was clicked by the user, in contrast to it being just some default menu item selected.
    public bool IsActivelySelected
    {
        get => _isActivelySelected;
        set
        {
            if (_isActivelySelected == value) return;
            
            _isActivelySelected = value;
            OnPropertyChanged();
            if (value)
            {
                Classes.Add(ItemSelectedClass);
            }
            else
            {
                Classes.Remove(ItemSelectedClass);
            }
        }
    }

    private void OnItemClick(MainMenuItemView obj)
    {
        SelectItem(obj.Header);
    }

    private void SelectItem(string header)
    {
        IsActivelySelected = header != null;

        foreach (var item in _menuItems)
        {
            item.IsSelected = header == null ? item.Header == DefaultItem : item.Header == header;
            if (item.IsSelected)
            {
                SelectedItem = item.Header;
            }
        }
    }

    private void Close()
    {
        Commands.View.HideMainMenuCommand.Execute();
    }

    private void Back()
    {
        SelectItem(null);
    }

    private void Save()
    {
        Close();
        Commands.File.Save.Execute();
    }
}