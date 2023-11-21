using System;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract.Services;
using Pix2d.Services;
using Pix2d.State;

namespace Pix2d.Desktop.Services;

public class UwpReviewService : ReviewService
{
    public override async Task<bool> RateApp()
    {
        var appId = "9nblggh1zdfv";
        //await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=" + appId));

        var result = await RateWithSystemDialogApp();
        return result;
    }

    private async Task<bool> RateWithSystemDialogApp()
    {
        var result = await UwpPlatformStuffService.WindowsStoreContext.RequestRateAndReviewAppAsync();
        return result.WasUpdated;
    }

    public UwpReviewService(ISettingsService settingsService, IMessenger messenger, AppState appState) : base(settingsService, messenger, appState)
    {
    }
}