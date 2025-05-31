using System;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract.Services;
using Pix2d.Services;
using Pix2d.State;

namespace Pix2d.Droid.Services;

public class AndroidReviewService(ISettingsService settingsService, IMessenger messenger, AppState appState)
    : ReviewService(settingsService, messenger, appState)
{

    public override async Task<bool> RateApp()
    {
        try
        {
            var appId = "com.pix2d.pix2dapp";

            var isInAppReviewShown = SettingsService.Get<bool?>("isInAppReviewShown");

            if (isInAppReviewShown == true)
            {
                Plugin.StoreReview.CrossStoreReview.Current.OpenStoreReviewPage(appId);
            }
            else
            {
                SettingsService.Set("isInAppReviewShown", true);
                await Plugin.StoreReview.CrossStoreReview.Current.RequestReview(false);
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        return false;
    }
}