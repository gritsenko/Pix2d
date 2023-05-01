using Avalonia.Styling;
using CommonServiceLocator;
using Mvvm;
using Pix2d.Mvvm;
using Pix2d.ViewModels.MainMenu;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Windows.Input;

namespace Pix2d.Views.MainMenu;

public class MainMenuItemView : ComponentBase
{
    protected override object Build() =>
        new Button()
            .BindClass(this.IsSelected, "selected")
            .FontSize(16)
            .Content(
                new Grid().Cols("32,*")
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .Children(
                        new TextBlock().Col(0) //icon
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            //.IsVisible(!itemVm.IsSplitter)
                            .Text(Bind(Icon)),
                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(Bind(Header))
                    )
            )
            .Padding(8, 8, 8, 8)
            .HorizontalContentAlignment(HorizontalAlignment.Left)
            .OnClick(_ => { OnClicked(this); })
            .CommandParameter(this);

    private object? _tabContentInstance;
    public event EventHandler<MainMenuItemView>? Clicked;
    public bool IsSelected { get; set; }
    public string Header { get; set; } = "null!";
    public string Icon { get; set; }
    public Type? TabViewType { get; set; }

    protected virtual void OnClicked(MainMenuItemView e)
    {
        Clicked?.Invoke(this, e);
    }

    public ComponentBase? GetTabContent()
    {
        if (TabViewType == null)
            return null;

        _tabContentInstance ??= Activator.CreateInstance(TabViewType);
        return _tabContentInstance as ComponentBase;
    }
}
public class MainMenuView : ComponentBase
{
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

        return new Border()
            .Background(Brushes.DarkGray)
            .Child(
                new Grid().Cols("200,*")
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .Children(
                        new ItemsControl()
                            //.ItemTemplate(_menuItemTemplate)
                            .Items(
                                new MainMenuItemView()
                                    .Header("New")
                                    .Icon("0xE7C3")
                                    .OnClicked(OnItemClick)
                                    .TabViewType(typeof(NewDocumentView)),
                                
                                new MainMenuItemView()
                                    .Header("Open")
                                    .Icon("0xED41")
                                    .OnClicked(OnItemClick)
                                    .TabViewType(typeof(OpenDocumentView)),
                                
                                new MainMenuItemView()
                                    .Header("Save")
                                    .Icon("0xE74E")
                                    .OnClicked(OnItemClick),

                                new MainMenuItemView()
                                    .Header("Save as")
                                    .Icon("0xE792")
                                    .OnClicked(OnItemClick),

                                new MainMenuItemView()
                                    .Header("Community\nand support")
                                    .Icon("0xE8F2")
                                    .OnClicked(OnItemClick)
                            ),
                        new Border().Col(1)
                            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                            .Child(
                                new ContentControl()
                                    .Ref(out _tabContent)
                                    .DataTemplates(
                                        SaveFileTemplate,
                                        SupportTemplate
                                    )
                                    .Content(Bind(SelectedTab))
                            )
                    )
            );
    }

    private void OnItemClick(MainMenuItemView obj)
    {
        _tabContent.Content = obj.GetTabContent();
    }

    public static FuncDataTemplate<SaveDocumentViewModel> SaveFileTemplate =>
        new((vm, ns) => new SaveDocumentView(vm));

    public static FuncDataTemplate<SupportViewModel> SupportTemplate => new((vm, ns) =>
        new Grid().Children(
            new Button()
                .VerticalAlignment(VerticalAlignment.Center)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .Content("https://boosty.to/pix2d")
                .OnClick(args =>
                {
                    ServiceLocator.Current.GetInstance<IPlatformStuffService>().OpenUrlInBrowser("https://boosty.to/pix2d");
                })
        )
    );

    [Inject] private ILicenseService LicenseService { get; set; }
    private IViewModelService ViewModelService { get; }
    [Inject] private AppState AppState { get; set; }
    [Inject] private IProjectService ProjectService { get; } = null!;

    ContentControl _tabContent;

    private bool _wideMode;
    private ItemModel _selectedMenuItem;
    private MenuItemDetailsViewModelBase _selectedTab;


    public ObservableCollection<ItemModel> MenuItems { get; set; } = new();

    public ItemModel SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            if (Equals(value, _selectedMenuItem)) return;
            _selectedMenuItem = value;
            OnPropertyChanged();
        }
    }

    public MenuItemDetailsViewModelBase SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (Equals(value, _selectedTab)) return;
            _selectedTab = value;
            OnPropertyChanged();
        }
    }

    //public IRelayCommand ItemSelectCommand => GetCommand<MainMenuItemViewModel>(SetCurrentItem);
    protected override void OnAfterInitialized()
    {
        Load();
    }

    protected void Load()
    {
        if (!MenuItems.Any())
        {
            MenuItems.Add(new ItemModel("Back")
            {
                IconGlyph = (char)0xE72B,
                OnSelectAction = () =>
                {
                    if (_wideMode || SelectedTab == null)
                    {
                        CloseMenu();
                    }
                    else SelectedTab = null;

                    SelectedMenuItem = null;
                }
            });
            //MenuItems.Add(new ItemModel("Open", typeof(OpenDocumentViewModel))
            //{ IconGlyph = (char)0xED41 });
            MenuItems.Add(new ItemModel("Save")
            {
                IconGlyph = (char)0xE74E,
                OnSelectAction = async () =>
                {
                    await ProjectService.SaveCurrentProjectAsync();
                    CloseMenu();
                    SelectedMenuItem = null;
                }
            });
            MenuItems.Add(new ItemModel("Save as", typeof(SaveDocumentViewModel))
            { IconGlyph = (char)0xE792 });

            MenuItems.Add(new ItemModel("") { IsSplitter = true });

            if (LicenseService?.AllowBuyPro == true)
            {
                MenuItems.Add(new ItemModel("License", typeof(LicenseViewModel))
                { IconGlyph = (char)0xE719 });
            }

            MenuItems.Add(new ItemModel("Community\nand support", typeof(SupportViewModel))
            { IconGlyph = (char)0xE8F2 });

            //MenuItems.Add(new ItemModel("Settings", typeof(SettingsViewModel))
            //{ IconGlyph = (char)0xE713 });

            foreach (var itemModel in MenuItems)
            {
                itemModel.SelectCommand = new RelayCommand(() => SetCurrentItem(itemModel));
            }
        }
        OnPropertyChanged(nameof(MenuItems));

        if (AppState.UiState.ShowMenu)
        {
            OnMenuShown();
        }
    }

    public void OnMenuShown()
    {
        //if (_wideMode)
        //    SetCurrentItem(MenuItems.FirstOrDefault(x => x.DetailsViewModel == typeof(OpenDocumentViewModel)));
        //else
        //    SetCurrentItem(null);
    }

    private void SetCurrentItem(ItemModel item)
    {
        SelectedMenuItem = item;
        foreach (var mainMenuItemViewModel in MenuItems)
        {
            mainMenuItemViewModel.IsSelected = mainMenuItemViewModel == item;
        }

        //order is important
        if (item != null)
        {
            item.OnSelectAction?.Invoke();
        }

        if (item?.DetailsViewModel != null)
        {
            SelectedTab = ViewModelService.GetViewModel(item.DetailsViewModel) as MenuItemDetailsViewModelBase;
        }
        else
        {
            SelectedTab = null;
        }

    }

    //public void UpdateWideMode(bool wide)
    //{
    //    _wideMode = wide;
    //    if (wide && SelectedTab == null && SelectedMenuItem?.DetailsViewModel != null)
    //    {
    //        SelectedTab = ViewModelService.GetViewModel(SelectedMenuItem.DetailsViewModel) as MenuItemDetailsViewModelBase;
    //    }
    //    else if (wide)
    //    {
    //        SelectedTab = ViewModelService.GetViewModel<OpenDocumentViewModel>();
    //    }

    //    if (!wide)
    //    {
    //        SetCurrentItem(null);
    //    }
    //}

    public void CloseMenu()
    {
        Commands.View.HideMainMenuCommand.Execute();
    }

    //public async void SelectLicenseSection()
    //{
    //    var licenseItem = MenuItems.FirstOrDefault(x => x.DetailsViewModel == typeof(LicenseViewModel));
    //    if (licenseItem == null)
    //        return;

    //    await Task.Delay(500);
    //    //this.ItemSelectCommand.Execute(licenseItem);
    //}

    //public void OnHardwareBackButtonPressed()
    //{
    //    MenuItems[0].OnSelectAction.Invoke();
    //}

    public class ItemModel : ObservableObject
    {
        public Type DetailsViewModel { get; }
        public string Name { get; set; }
        public char IconGlyph { get; set; }

        public IRelayCommand SelectCommand { get; set; }

        public Action OnSelectAction { get; set; }

        public bool IsSelected
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool IsSplitter { get; set; }

        public ItemModel(string name, Type detailsVm = null)
        {
            DetailsViewModel = detailsVm;
            //DetailsViewModel.Name = name;
            Name = name;
        }
    }

}