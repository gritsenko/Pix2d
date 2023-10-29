using System.Threading.Tasks;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class GumroadLicenseService : ILicenseService
{
    public AppState AppState { get; }
    public string GetFormattedPrice { get; } = "$9.9";
    public async Task<bool> BuyPro()
    {
        return true;
    }

    public GumroadLicenseService(AppState appState)
    {
        AppState = appState;
    }

    public void ToggleIsPro()
    {
        AppState.LicenseType = AppState.IsPro ? LicenseType.Essentials : LicenseType.Pro;
        Logger.Log("$On license changed to " + (AppState.IsPro ? "PRO" : "ESS"));
    }

}