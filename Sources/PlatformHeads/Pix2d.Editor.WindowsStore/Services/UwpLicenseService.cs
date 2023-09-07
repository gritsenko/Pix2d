using System;
using System.Collections.Generic;
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
    public event EventHandler LicenseChanged;

    public bool AllowBuyPro { get; } = true;
    public bool IsPro { get; private set; }
    //#if DEBUG
    //            = false;
    //#endif

    public UwpLicenseService()
    {
        Init();
    }

    public async void Init()
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
        try
        {

            _licenseInformation = CurrentApp.LicenseInformation;
#if DEBUG
            //                // The next line is commented out for production/release.       
            _licenseInformation = CurrentAppSimulator.LicenseInformation;
#endif

            var proSetting = ApplicationData.Current.LocalSettings.Values["IsProActivated"];

            var isPro = false;

            if (proSetting != null)
            {
                Logger.Log("initialized Pro from settings");
                isPro = true;
            }

            if (_licenseInformation.ProductLicenses["proVersion"].IsActive)
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
                var listing = await CurrentApp.LoadListingInformationAsync();
                ProductListing productInfo = null;
                if (listing.ProductListings.TryGetValue("proVersion", out productInfo))
                {
                    FormattedPrice = productInfo?.FormattedPrice;
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
        finally
        {
            Pix2DApp.Instance.CurrentLicense = IsPro ? "pro" : "free";
        }
    }

    public async Task<bool> CheckIsPaid()
    {
        try
        {
            if (!_licenseInformation.IsActive)
                return false;

            var result = await CurrentApp.GetProductReceiptAsync("9NBLGGH1ZDFV");

            if (string.IsNullOrEmpty(result))
                return false;

            return true;
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
            //var result = await CurrentApp.RequestAppPurchaseAsync(false);
            var result = await CurrentApp.RequestProductPurchaseAsync("proVersion");
            if (result.Status == ProductPurchaseStatus.Succeeded ||
                result.Status == ProductPurchaseStatus.AlreadyPurchased)
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
