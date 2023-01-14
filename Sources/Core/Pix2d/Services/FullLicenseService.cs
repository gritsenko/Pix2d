using System;
using System.Threading.Tasks;
using Pix2d.Abstract;

namespace Pix2d.Services
{
    public class FullLicenseService : ILicenseService
    {
        public event EventHandler LicenseChanged;
        public string FormattedPrice { get; } = "$9.9";
        public bool AllowBuyPro { get; } = false;
        public bool IsPro { get; } = true;
        public async Task<bool> BuyPro()
        {
            return true;
        }

        public void ToggleIsPro()
        {
            
        }

        public async Task<bool> RateApp()
        {
            //await Launcher.LaunchUriAsync(new Uri("https://gritsenko.itch.io/pix2d"));
            return true;
        }
    }
}