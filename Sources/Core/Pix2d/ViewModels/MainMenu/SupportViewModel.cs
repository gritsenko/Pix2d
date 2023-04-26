using System.Threading.Tasks;
using Mvvm;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.MainMenu
{
    public class SupportViewModel : MenuItemDetailsViewModelBase
    {
        public ILicenseService LicenseService { get; }

        public IRelayCommand RateAppCommand => GetCommand(async () =>
        {
            await Task.Delay(300);
            await LicenseService.RateApp();
        });

        public SupportViewModel(ILicenseService licenseService)
        {
            LicenseService = licenseService;
        }

        protected override void OnLoad()
        {
            Logger.Log("Support view opened");
        }
    }
}