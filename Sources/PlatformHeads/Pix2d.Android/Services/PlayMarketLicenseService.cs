using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using Pix2d.State;
using Plugin.InAppBilling;

namespace Pix2d.Services;

public class PlayMarketLicenseService : ILicenseService, IInAppBillingVerifyPurchase
{
    public AppState AppState { get; }
    public ISettingsService SettingsService => CoreServices.SettingsService;
    public string GetFormattedPrice { get; set; } = $"9.99";

    public PlayMarketLicenseService(AppState appState)
    {
        AppState = appState;
        InitAsync();
    }

    public async void InitAsync()
    {

        try
        {
            //#if DEBUG
            Logger.Log("Checking Pro version and price");
            await CheckIsPro();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public async Task<bool> CheckIsPro(bool proposeUpgrade = true)
    {
        var billing = CrossInAppBilling.Current;
        try
        {

            var productIds = new string[] { "pix2d_pro" };
            //You must connect
            var connected = await billing.ConnectAsync(true);

            if (!connected)
            {
                //Couldn't connect
                return false;
            }

            var proSetting = SettingsService.Get<bool>("IsProActivated");

            var isPro = false;

            if (proSetting != null)
            {
                Logger.Log("initialized Pro from settings");
                isPro = true;
            }

            var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);

            //check for null just incase
            if (purchases?.Any(p => p.ProductId == productIds[0]) ?? false)
            {
                //Purchase restored
                Logger.Log("initialized Pro from license information");
                isPro = true;
            }
            else
            {
                //no purchases found
                isPro = false;
            }

            if (isPro)
            {
                //save to app settings
                SettingsService.Set("IsProActivated", true);
                AppState.LicenseType = LicenseType.Pro;
            }

            if (!isPro)
            {
                var items = await billing.GetProductInfoAsync(ItemType.InAppPurchase, productIds);
                var item = items.FirstOrDefault();
                GetFormattedPrice = item.LocalizedPrice;
            }

            return isPro;
        }
        catch (InAppBillingPurchaseException e)
        {
            Logger.LogEventWithParams("$ Error getting billing app info", new Dictionary<string, string>
            {
                ["type"] = e.PurchaseError.ToString(),
                ["message"] = e.Message,
                ["stack trace"] = e.StackTrace,
            });
        }
        catch (Exception ex)
        {
            HandleCheckProException(ex);
        }
        finally
        {
            await billing.DisconnectAsync();
        }

        return false;
    }

    private void HandleCheckProException(Exception ex)
    {
        Logger.LogException(ex);
        var dlgService = ServiceLocator.Current.GetInstance<IDialogService>();
        dlgService?.Alert("Can't get license information from Play Market. Error: " + ex.Message + ". Please try to restart Pix2d. Contact to the developer, if this problem still persist.", "Getting license error");
    }

    public async Task<bool> BuyPro()
    {
        try
        {
            if (AppState.IsPro)
                return false;

            var productId = "pix2d_pro";

            var connected = await CrossInAppBilling.Current.ConnectAsync();

            if (!connected)
            {
                //Couldn't connect to billing, could be offline, alert user
                var dlgService = ServiceLocator.Current.GetInstance<IDialogService>();
                dlgService.Alert("Can't connect to google play services.", "Network problem");
                return false;
            }

            //try to purchase item
            var purchase = await CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase);
            if (purchase == null)
            {

                Logger.LogEvent($"$ Buy canceled: purchase == null");

                //Not purchased, alert the user
            }
            else
            {
                //Purchased, save this information
                var id = purchase.Id;
                var token = purchase.PurchaseToken;
                var state = purchase.State;

                SettingsService.Set("purchaseToken", token);
                SettingsService.Set("purchaseId", id);
                SettingsService.Set("purchaseState", state.ToString());

                if (state == PurchaseState.Purchased
                    || state == PurchaseState.Restored
                    || state == PurchaseState.Purchasing)
                {
                    AppState.LicenseType = LicenseType.Pro;
                    SettingsService.Set("IsProActivated", true);
                    Logger.LogEvent($"$ Pro version bought: {state}");

                    if (state == PurchaseState.Purchased)
                    {
                        var result =
                            await CrossInAppBilling.Current.FinalizePurchaseOfProductAsync(new[] {productId});
                        if (result.FirstOrDefault().Success)
                        {
                            Logger.LogEvent($"$ Purchase acknowledged");
                        }
                    }

                    return true;
                }
                else
                {
                    Logger.LogEvent($"$ Not purchased: {state}");
                }
            }
        }
        catch (InAppBillingPurchaseException e)
        {
            Logger.LogEventWithParams("$ Purchase error", new Dictionary<string, string>
            {
                ["type"] = e.PurchaseError.ToString(),
                ["message"] = e.Message,
                ["stack trace"] = e.StackTrace,
            });
            
            if (e.PurchaseError == PurchaseError.BillingUnavailable)
            {
                return await ShowBackdoorForSanctionedUsers();
            }

            return false;
        }
        catch (Exception e)
        {
            Logger.LogException(e);
            Logger.LogEvent($"$ Exception while buying:\n{e.Message}\n{e.StackTrace}");

            //Something bad has occurred, alert user
            return false;
        }
        finally
        {
            //Disconnect, it is okay if we never connected
            await CrossInAppBilling.Current.DisconnectAsync();
        }

        Logger.LogEvent($"$ Buy cancelled: unknown reason");

        return false;
    }

    private async Task<bool> ShowBackdoorForSanctionedUsers()
    {
        Logger.LogEvent($"$ Show cheat code dialog");
        
        var dlgService = ServiceLocator.Current.GetInstance<IDialogService>();
        var userCode = await dlgService.ShowInputDialogAsync(
            "It seems that billing service does not work in your region. " +
            "Please, contact us at igor@pix2d.com to get a promo code to " +
            "enjoy Pix2d PRO.",
            "Google billing unavailable");

        if (userCode == "idkfa")
        {
            AppState.LicenseType = LicenseType.Pro;
            SettingsService.Set("IsProActivated", true);
            Logger.LogEvent($"$ Pro version activated through cheat code");
            await dlgService.ShowAlert("Thank you. You are a PRO now!", "PRO activated");
            return true;
        }

        if (userCode == null)
        {
            return false;
        }

        Logger.LogEvent($"$ Invalid cheat code entered");
        await dlgService.ShowAlert("Sorry, your promo code is not valid.", "Promo code is not valid");
        return false;
    }

    public async Task<bool> RateApp()
    {
        try
        {
            var appId = "com.pix2d.pix2dapp";

            var isInAppReviewShown = SettingsService.Get<bool>("isInAppReviewShown");

            if (isInAppReviewShown != null)
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

    public async Task<bool> VerifyPurchase(string signedData, string signature, string productId = null, string transactionId = null)
    {
        return true;
    }
}