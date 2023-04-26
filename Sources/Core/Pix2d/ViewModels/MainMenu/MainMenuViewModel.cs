using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Mvvm;
using Pix2d.Abstract.UI;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.MainMenu
{
    public class MainMenuViewModel : Pix2dViewModelBase
    {
        private IMenuController MenuController { get; }
        private ILicenseService LicenseService { get; }
        public IViewModelService ViewModelService { get; }

        private bool _wideMode;

        private IProjectService ProjectService => CoreServices.ProjectService;

        public ObservableCollection<MainMenuItemViewModel> MenuItems { get; set; } = new ObservableCollection<MainMenuItemViewModel>();

        public MainMenuItemViewModel SelectedMenuItem
        {
            get => Get<MainMenuItemViewModel>();
            set => Set(value);
        }

        public MenuItemDetailsViewModelBase SelectedTab
        {
            get => Get<MenuItemDetailsViewModelBase>();
            set => Set(value);
        }

        [NotifiesOn(nameof(SelectedTab))]
        public bool ShowTabContent => SelectedTab != null;

        public IRelayCommand ItemSelectCommand => GetCommand<MainMenuItemViewModel>(SetCurrentItem);
        public MainMenuViewModel(IMenuController menuController, ILicenseService licenseService, IViewModelService viewModelService)
        {
            if (IsDesignMode)
                return;

            MenuController = menuController;
            LicenseService = licenseService;
            ViewModelService = viewModelService;
            SelectedTab = null;
        }

        protected override void OnLoad()
        {
            if (!MenuItems.Any())
            {
                MenuItems.Add(new MainMenuItemViewModel("Back")
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
                MenuItems.Add(new MainMenuItemViewModel("New", typeof(NewDocumentSettingsViewModel))
                { IconGlyph = (char)0xE7C3 });
                MenuItems.Add(new MainMenuItemViewModel("Open", typeof(OpenDocumentViewModel))
                { IconGlyph = (char)0xED41 });
                MenuItems.Add(new MainMenuItemViewModel("Save")
                {
                    IconGlyph = (char)0xE74E,
                    OnSelectAction = async () =>
                    {
                        await ProjectService.SaveCurrentProjectAsync();
                        CloseMenu();
                        SelectedMenuItem = null;
                    }
                });
                MenuItems.Add(new MainMenuItemViewModel("Save as", typeof(SaveDocumentViewModel))
                { IconGlyph = (char)0xE792 });

                MenuItems.Add(new MainMenuItemViewModel("") { IsSplitter = true });

                if (LicenseService?.AllowBuyPro == true)
                {
                    MenuItems.Add(new MainMenuItemViewModel("License", typeof(LicenseViewModel))
                    { IconGlyph = (char)0xE719 });
                }

                MenuItems.Add(new MainMenuItemViewModel("Community\nand support", typeof(SupportViewModel))
                { IconGlyph = (char)0xE8F2 });

                //MenuItems.Add(new MainMenuItemViewModel("Settings", typeof(SettingsViewModel))
                //{ IconGlyph = (char)0xE713 });

                foreach (var mainMenuItemViewModel in MenuItems)
                {
                    mainMenuItemViewModel.SelectCommand = ItemSelectCommand;
                }
            }
            OnPropertyChanged(nameof(MenuItems));

            if (MenuController.ShowMenu)
            {
                OnMenuShown();
            }
        }

        public void OnMenuShown()
        {
            if (_wideMode)
                SetCurrentItem(MenuItems.FirstOrDefault(x => x.DetailsViewModel == typeof(OpenDocumentViewModel)));
            else
                SetCurrentItem(null);
        }

        private void SetCurrentItem(MainMenuItemViewModel item)
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

        public void UpdateWideMode(bool wide)
        {
            _wideMode = wide;
            if (wide && SelectedTab == null && SelectedMenuItem?.DetailsViewModel != null)
            {
                SelectedTab = ViewModelService.GetViewModel(SelectedMenuItem.DetailsViewModel) as MenuItemDetailsViewModelBase;
            }
            else if (wide)
            {
                SelectedTab = ViewModelService.GetViewModel<OpenDocumentViewModel>();
            }

            if (!wide)
            {
                SetCurrentItem(null);
            }
        }

        public void CloseMenu()
        {
            MenuController.ShowMenu = false;
        }

        public async void SelectLicenseSection()
        {
            var licenseItem = MenuItems.FirstOrDefault(x => x.DetailsViewModel == typeof(LicenseViewModel));
            if (licenseItem == null)
                return;
            
            await Task.Delay(500);
            this.ItemSelectCommand.Execute(licenseItem);
        }

        public void OnHardwareBackButtonPressed()
        {
            MenuItems[0].OnSelectAction.Invoke();
        }
    }
}
