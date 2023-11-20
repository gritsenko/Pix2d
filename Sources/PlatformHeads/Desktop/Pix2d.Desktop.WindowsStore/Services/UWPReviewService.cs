using System;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract.Services;
using Pix2d.Services;

namespace Pix2d.Desktop.Services;

public class UwpReviewService : ReviewService
{
    public UwpReviewService(ISettingsService settingsService, IMessenger messenger) : base(settingsService, messenger)
    {
    }

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

}