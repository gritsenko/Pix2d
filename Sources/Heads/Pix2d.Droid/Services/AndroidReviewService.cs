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

    protected override async Task<bool> RateAppCore()
    {
        try
        {
            var appId = "com.pix2d.pix2dapp";

            SettingsService.TryGet<bool>(nameof(AppSettings.IsInAppReviewShown), out var isInAppReviewShown);

            if (isInAppReviewShown)
            {
                Plugin.StoreReview.CrossStoreReview.Current.OpenStoreReviewPage(appId);
                LogReview("Opened store page");
            }
            else
            {
                SettingsService.Set(nameof(AppSettings.IsInAppReviewShown), true);
                // Google's in-app review API surfaces no outcome (it may even show nothing if quota-limited),
                // so "requested" is the strongest signal we can log — the dialog was asked for.
                await Plugin.StoreReview.CrossStoreReview.Current.RequestReview(false);
                LogReview("In-app review requested");
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