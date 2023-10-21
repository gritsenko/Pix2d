using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Storage;
using Windows.UI.Popups;
using CommonServiceLocator;
using Pix2d.Abstract.Services;
using Windows.Services.Store;

namespace Pix2d.WindowsStore.Services;

public class UwpLicenseService : ILicenseService
{
    public string FormattedPrice { get; set; } = "$4.99";
    private LicenseInformation _licenseInformation;
    private StoreContext _context;
    private IntPtr? _hwnd;

    public event EventHandler LicenseChanged;

    public bool AllowBuyPro { get; } = true;
    public bool IsPro { get; private set; }
    //#if DEBUG
    //            = false;
    //#endif

    public UwpLicenseService()
    {
        //Init();
    }

    public async Task Init()
    {

        try
        {
            //#if DEBUG
            Logger.Log("Checking Pro version and price");
            IsPro = await CheckIsPro();
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
            _context = StoreContext.GetDefault();

            //old api
            //_licenseInformation = CurrentApp.LicenseInformation;
#if DEBUG
            //                // The next line is commented out for production/release.       
            //_licenseInformation = CurrentAppSimulator.LicenseInformation;
#endif

            var proSetting = ApplicationData.Current.LocalSettings.Values["IsProActivated"];

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
                ApplicationData.Current.LocalSettings.Values["IsProActivated"] = true;
                IsPro = true;
                OnLicenseChanged();
            }

            if (!isPro)
            {
                // Specify the kinds of add-ons to retrieve.
                string[] productKinds = { "Durable" };
                List<String> filterList = new List<string>(productKinds);

                {
                    StoreProductQueryResult queryResult = await _context.GetUserCollectionAsync(filterList);

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
                            ApplicationData.Current.LocalSettings.Values["IsProActivated"] = true;
                            return true;
                        }
                        // Use members of the product object to access info for the product...
                    }
                }

                //check available in-apps
                {
                    StoreProductQueryResult queryResult = await _context.GetAssociatedStoreProductsAsync(filterList);

                    foreach (KeyValuePair<string, StoreProduct> item in queryResult.Products)
                    {
                        StoreProduct product = item.Value;
                        if (product.InAppOfferToken == "proVersion")
                        {
                            FormattedPrice = product?.Price?.FormattedPrice ?? "$10";
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
            var license = await _context.GetAppLicenseAsync();
            
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
        Logger.LogEventWithParams("$ Buy Pro", new Dictionary<string, string>() { { "Price", FormattedPrice } });
        try
        {
            if (_hwnd == null)
            {
                _hwnd = EditorApp.TopLevel.TryGetPlatformHandle()?.Handle;

                if (_hwnd != null)
                {
                    // Initialize the dialog using wrapper funcion for IInitializeWithWindow
                    WinRT.Interop.InitializeWithWindow.Initialize(_context, _hwnd.Value);
                }

            }

            
            //await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            //{
            //    var _result = await CurrentApp.RequestProductPurchaseAsync("proVersion");
            //});
            //var result = await CurrentApp.RequestAppPurchaseAsync(false);
            //var result = await CurrentApp.RequestProductPurchaseAsync("proVersion");

            string[] productKinds = { "Durable" };
            List<String> filterList = new List<string>(productKinds);
            StoreProductQueryResult queryResult = await _context.GetAssociatedStoreProductsAsync(filterList);
            StoreProduct? product = null;

            foreach (KeyValuePair<string, StoreProduct> item in queryResult.Products)
            {
                product = item.Value;
                if (product.InAppOfferToken == "proVersion")
                {
                    FormattedPrice = product?.Price?.FormattedPrice ?? "$10";
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
                IsPro = true;
                ApplicationData.Current.LocalSettings.Values["IsProActivated"] = true;
                Logger.LogEvent($"$ Pro version bought: {result.Status}");
                OnLicenseChanged();
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

    public void ToggleIsPro()
    {
        IsPro = !IsPro;

        Logger.Log("$On license changed to " + (IsPro ? "PRO" : "ESS"));
        OnLicenseChanged();
    }

    public async Task<bool> RateApp()
    {
        var appId = "9nblggh1zdfv";
        //await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=" + appId));

        var result = await RateWithSystemDialogApp();
        return result;
    }

    private async Task<bool> RateWithSystemDialogApp()
    {
#if WINDOWS_UWP
        var result = await StoreContext.GetDefault().RequestRateAndReviewAppAsync();
        return result.WasUpdated;
#else
        throw new NotImplementedException();
#endif
    }

    private void CommandInvokedHandler(IUICommand command)
    {
        // Display message showing the label of the command that was invoked
        //            rootPage.NotifyUser("The '" + command.Label + "' command has been selected.",
        //                NotifyType.StatusMessage);
    }


    public void OnLicenseChanged()
    {
        LicenseChanged?.Invoke(null, EventArgs.Empty);
    }
}
