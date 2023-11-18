using System.Threading.Tasks;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class FullLicenseService : ILicenseService
{
    public AppState AppState { get; }
    public string GetFormattedPrice { get; } = "$9.9";
    public async Task<bool> BuyPro()
    {
        return true;
    }

    public FullLicenseService(AppState appState)
    {
        AppState = appState;
        AppState.LicenseType = LicenseType.Pro;
    }

}