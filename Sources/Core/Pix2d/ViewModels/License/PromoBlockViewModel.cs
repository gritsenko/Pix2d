using System;
using System.Windows.Input;
using Pix2d.Mvvm;
using Pix2d.ViewModels.MainMenu;

namespace Pix2d.ViewModels.License
{
    public class PromoBlockViewModel : Pix2dViewModelBase
    {
        public ILicenseService LicenseService { get; }
        public IViewModelService ViewModelService { get; }
        public string CallToActionText
        {
            get => Get<string>(); 
            set => Set(value);
        } 
        public string TimerText
        {
            get => Get<string>(); 
            set => Set(value);
        }
        public bool ShowTimer
        {
            get => Get<bool>();
            set => Set(value);
        }

        public ICommand ShowLicenseInfoCommand => GetCommand(() =>
        {
            Logger.Log("$Pressed to promo block");

            if (LicenseService == null)
                return;

            Commands.View.ShowMainMenuCommand.Execute();
            var mainMenu = ViewModelService.GetViewModel<MainMenuViewModel>();
            mainMenu.SelectLicenseSection();
        });

        public PromoBlockViewModel(ILicenseService licenseService, IViewModelService viewModelService)
        {
            if (IsDesignMode || licenseService == null)
                return;

            LicenseService = licenseService;
            ViewModelService = viewModelService;
            LicenseService.LicenseChanged += LicenseService_LicenseChanged;
        }

        private void LicenseService_LicenseChanged(object sender, EventArgs e)
        {
            OnLoad();
        }

        protected override void OnLoad()
        {
            var isPro = LicenseService?.IsPro ?? true;
            CallToActionText = isPro ? "PRO" : "ESS";

            if (isPro)
            {
                TimerText = "14 days left";
            }
        }
    }
}
