using System;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Services;
using Plugin.InAppBilling;

namespace Pix2d.Services
{
    public class PlayMarketLicenseService : ILicenseService, IInAppBillingVerifyPurchase
    {
        public ISettingsService SettingsService => CoreServices.SettingsService;

        public event EventHandler LicenseChanged;
        public string FormattedPrice { get; set; } = $"9.99";

        public bool AllowBuyPro { get; } = true;
        public bool IsPro { get; set; } = false;

        public PlayMarketLicenseService()
        {
        }

        public async Task Init()
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
                    IsPro = true;
                    OnLicenseChanged();
                }

                if (!isPro)
                {
                    var items = await billing.GetProductInfoAsync(ItemType.InAppPurchase, productIds);
                    var item = items.FirstOrDefault();
                    FormattedPrice = item.LocalizedPrice;
                }

                return isPro;
            }
            catch (InAppBillingPurchaseException pEx)
            {
                HandleCheckProException(pEx);
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
            dlgService?.Alert("Can't get license information from Windows Store. Error: " + ex.Message + ". Please try to restart Pix2d. Contact to the developer, if this problem still persist.", "Getting license error");
        }

        public void OnLicenseChanged()
        {
            LicenseChanged?.Invoke(null, EventArgs.Empty);
        }

        public async Task<bool> BuyPro()
        {

            try
            {
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
                        IsPro = true;
                        SettingsService.Set("IsProActivated", true);
                        Logger.LogEvent($"$ Pro version bought: {state}");

                        if (state == PurchaseState.Purchased)
                        {
                            var result =
                                await CrossInAppBilling.Current.FinalizePurchaseOfProductAsync(new[] { productId });
                            if (result.FirstOrDefault().Success)
                            {
                                Logger.LogEvent($"$ Purchase acknowledged");
                            }
                        }

                        OnLicenseChanged();
                        return true;
                    }
                    else
                    {
                        Logger.LogEvent($"$ Not purchased: {state}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                Logger.LogEvent($"$ Exception while buying:\n{e.Message}\n{e.StackTrace}");

                //Something bad has occurred, alert user
            }
            finally
            {
                //Disconnect, it is okay if we never connected
                await CrossInAppBilling.Current.DisconnectAsync();
            }

            Logger.LogEvent($"$ Buy cancelled: unknown reason");

            return false;
        }

        //void handlePurchase(Purchase purchase)
        //{
        //    if (purchase.getPurchaseState() == PurchaseState.PURCHASED)
        //    {
        //        if (!purchase.isAcknowledged())
        //        {
        //            AcknowledgePurchaseParams acknowledgePurchaseParams =
        //                AcknowledgePurchaseParams.newBuilder()
        //                    .setPurchaseToken(purchase.getPurchaseToken())
        //                    .build();
        //            client.acknowledgePurchase(acknowledgePurchaseParams, acknowledgePurchaseResponseListener);
        //        }
        //    }
        //}
        public void ToggleIsPro()
        {
            IsPro = !IsPro;

            Logger.Log("$On license changed to " + (IsPro ? "PRO" : "ESS"));
            OnLicenseChanged();
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
}