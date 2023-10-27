using System.Threading.Tasks;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class GumroadLicenseService : ILicenseService
{
    public event EventHandler LicenseChanged;
    
    public LicenseType License { get; private set; }
    public bool IsPro => License == LicenseType.Pro || License == LicenseType.Ultimate;
    public string FormattedPrice { get; } = "$9.9";
    public bool AllowBuyPro { get; } = true;
    public async Task<bool> BuyPro()
    {
        return true;
    }

    public void ToggleIsPro()
    {
        License = IsPro ? LicenseType.Essentials : LicenseType.Pro;

        LicenseChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> RateApp()
    {
        //await Launcher.LaunchUriAsync(new Uri("https://gritsenko.itch.io/pix2d"));
        return true;
    }
}