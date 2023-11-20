using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Services.Store;
using Avalonia.Threading;
using CommonServiceLocator;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using Pix2d.State;

namespace Pix2d.Desktop.Services;

public class UwpLicenseService : ILicenseService
{
    public AppState AppState { get; }
    public ISettingsService SettingsService { get; }
    public string GetFormattedPrice { get; set; } = "$4.99";

    public UwpLicenseService(AppState appState, ISettingsService settingsService)
    {
        AppState = appState;
        SettingsService = settingsService;
        Dispatcher.UIThread.InvokeAsync(InitAsync);
    }

    public async Task InitAsync()
    {

        try
        {
            //#if DEBUG
            Logger.Log("Checking Pro version and price");
            AppState.LicenseType = await CheckIsPro() ? LicenseType.Pro : LicenseType.Essentials;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public async Task<bool> CheckIsPro(bool proposeUpgrade = true)
    {
        try
        {
            var context = UwpPlatformStuffService.WindowsStoreContext;

            //old api
            //_licenseInformation = CurrentApp.LicenseInformation;
#if DEBUG
            //                // The next line is commented out for production/release.       
            //_licenseInformation = CurrentAppSimulator.LicenseInformation;
#endif

            var proSetting = SettingsService.Get<bool>("IsProActivated");

            var isPro = false;

            if (proSetting != null)
            {
                Logger.Log("initialized Pro from settings");
                isPro = true;
            }

            if (await CheckIsPaidAsync())
            {
                Logger.Log("initialized Pro from license information");
                isPro = true;
            }

            if (isPro)
            {
                SettingsService.Set("IsProActivated", true);
                AppState.LicenseType = LicenseType.Pro;
            }

            if (!isPro)
            {
                // Specify the kinds of add-ons to retrieve.
                string[] productKinds = { "Durable" };
                List<String> filterList = new List<string>(productKinds);

                {
                    StoreProductQueryResult queryResult = await context.GetUserCollectionAsync(filterList);

                    if (queryResult.ExtendedError != null)
                    {
                        // The user may be offline or there might be some other server failure.
                        return isPro;
                    }

                    foreach (KeyValuePair<string, StoreProduct> item in queryResult.Products)
                    {
                        StoreProduct product = item.Value;
                        if (product.InAppOfferToken == "proVersion")
                        {
                            SettingsService.Set("IsProActivated", true);
                            return true;
                        }
                        // Use members of the product object to access info for the product...
                    }
                }

                //check available in-apps
                {
                    StoreProductQueryResult queryResult = await context.GetAssociatedStoreProductsAsync(filterList);

                    foreach (KeyValuePair<string, StoreProduct> item in queryResult.Products)
                    {
                        StoreProduct product = item.Value;
                        if (product.InAppOfferToken == "proVersion")
                        {
                            GetFormattedPrice = product?.Price?.FormattedPrice ?? "$10";
                        }
                        // Use members of the product object to access info for the product...
                    }
                }
            }

            return isPro;
        }
        catch (Exception e)
        {
            Logger.LogException(e);

            var dlgService = ServiceLocator.Current.GetInstance<IDialogService>();
            dlgService.Alert("Can't get license information from Windows Store. Error: " + e.Message + ". Please try to restart Pix2d. Contact to the developer, if this problem still persist.", "Getting license error");
            return false;
        }
    }

    public async Task<bool> CheckIsPaidAsync()
    {
        try
        {
            var context = UwpPlatformStuffService.WindowsStoreContext;
            var license = await context.GetAppLicenseAsync();
            
            if (!license.IsActive)
                return false;

            var hasLicense = license.AddOnLicenses.Keys.Contains("9NBLGGH1ZDFV");

            if (!hasLicense)
                return false;

            var result = license.AddOnLicenses.FirstOrDefault(x => x.Key == "9NBLGGH1ZDFV").Value;

            //var result = await CurrentApp.GetProductReceiptAsync("9NBLGGH1ZDFV");
            return result.IsActive;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        return false;
    }

    public async Task<bool> BuyPro()
    {
        Logger.LogEventWithParams("$ Buy Pro", new Dictionary<string, string>() { { "Price", GetFormattedPrice } });
        try
        {
            //await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            //{
            //    var _result = await CurrentApp.RequestProductPurchaseAsync("proVersion");
            //});
            //var result = await CurrentApp.RequestAppPurchaseAsync(false);
            //var result = await CurrentApp.RequestProductPurchaseAsync("proVersion");

            string[] productKinds = { "Durable" };
            List<String> filterList = new List<string>(productKinds);
            var context = UwpPlatformStuffService.WindowsStoreContext;
            StoreProductQueryResult queryResult = await context.GetAssociatedStoreProductsAsync(filterList);
            StoreProduct? product = null;

            foreach (KeyValuePair<string, StoreProduct> item in queryResult.Products)
            {
                product = item.Value;
                if (product.InAppOfferToken == "proVersion")
                {
                    GetFormattedPrice = product?.Price?.FormattedPrice ?? "$10";
                }
                // Use members of the product object to access info for the product...
            }

            if (product == null)
                return false;

            var result = await product.RequestPurchaseAsync();
            
            //var result = await _context.GetStoreProductsAsync("proVersion");
            if (result.Status == StorePurchaseStatus.Succeeded ||
                result.Status == StorePurchaseStatus.AlreadyPurchased)
            {
                AppState.LicenseType = LicenseType.Pro;
                SettingsService.Set("IsProActivated", true);
                Logger.LogEvent($"$ Pro version bought: {result.Status}");
                return true;
            }
            //if (_licenseInformation.IsActive && !_licenseInformation.IsTrial)
            //{
            //    OnLicenseChanged();
            //    return true;
            //}

            Logger.LogEvent($"$ Buy canceled:" +
                            $" {result.Status}");

            //if (await MessageBox.YesNo("Purchase was not complete :-( Do you want to try direct pay with paypal by special price: $4.99? "))
            //{
            //    Logger.LogEvent("$ Clicked alternative method");
            //    await Windows.System.Launcher.LaunchUriAsync(new Uri("http://pixelart.studio/Home/Buy"));
            //}
            //else
            //{
            //    Logger.LogEvent("$ Clicked NO in alternative method");
            //}

            return false;
        }
        catch (Exception e)
        {
            Logger.LogException(e);
            Logger.LogEvent($"$ Exception while buying:\n{e.Message}\n{e.StackTrace}");
        }

        Logger.LogEvent($"$ Buy cancelled: unknown reason");

        return false;
    }
}