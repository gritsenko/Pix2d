using System.Threading.Tasks;

namespace Pix2d.Services;

public class GumroadLicenseService : ILicenseService
{
    public event EventHandler LicenseChanged;
    public string FormattedPrice { get; } = "$9.9";
    public bool AllowBuyPro { get; } = false;
    public bool IsPro { get; private set; } = true;
    public async Task<bool> BuyPro()
    {
        return true;
    }

    public void ToggleIsPro()
    {
        IsPro = !IsPro;
        LicenseChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> RateApp()
    {
        //await Launcher.LaunchUriAsync(new Uri("https://gritsenko.itch.io/pix2d"));
        return true;
    }
}