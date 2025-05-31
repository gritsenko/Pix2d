#nullable enable
using System.Threading.Tasks;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class FullLicenseService : ILicenseService
{
    private AppState AppState { get; }
    public string? GetFormattedPrice => "$9.9";

    public Task<bool> BuyPro()
    {
        return Task.FromResult(true);
    }

    public FullLicenseService(AppState appState)
    {
        AppState = appState;
        AppState.LicenseType = LicenseType.Pro;
    }

}