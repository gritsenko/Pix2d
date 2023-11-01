using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Pix2d.Abstract.UI;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class GumroadLicenseService : ILicenseService
{
    private readonly IDialogService _dialogService;
    private readonly IBusyController _busyController;
    private readonly ISettingsService _settingsService;
    private readonly IPlatformStuffService _platformStuffService;
    public AppState AppState { get; }
    public string GetFormattedPrice { get; } = "$9.9";
    public async Task<bool> BuyPro()
    {
        OpenGumroadPurchasePage();
        var licenseCode = await RequestUserLicenseCode();
        if (string.IsNullOrEmpty(licenseCode))
        {
            return false;
        }

        var result = false;
        await _busyController.RunLongTaskAsync(async () =>
        {
            result = await VerifyLicenseCode(licenseCode, firstUsage: true);
        });

        return result;
    }

    private async Task<bool> VerifyLicenseCode(string licenseCode, bool firstUsage)
    {
        if (!CheckLicenseCodeFormat(licenseCode))
        {
            Logger.LogEventWithParams("Invalid license code", new Dictionary<string, string> { ["Entered code"] = licenseCode });
            ShowErrorMessage("Invalid license code");
            SetLicense(LicenseType.Essentials);
            return false;
        }
        
        using var client = new HttpClient();
        var uri = new Uri(
            $"https://api.gumroad.com/v2/licenses/verify?product_id=zakM2mRVGjf6SGUYlFgvQg==&license_key={licenseCode}&increment_uses_count={firstUsage}");
        var result = await client.PostAsync(uri, null);

        if (result.IsSuccessStatusCode || (result.StatusCode == HttpStatusCode.NotFound && result.Content.Headers.ContentType?.MediaType == "application/json"))
        {
            var content = await result.Content.ReadAsStringAsync();
            try
            {
                var parsedResponse = JsonConvert.DeserializeObject<LicenseInfoResponse>(content, new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver()
                    {
                        NamingStrategy = new SnakeCaseNamingStrategy {ProcessDictionaryKeys = true},
                    },
                });

                if (parsedResponse.Success && parsedResponse.Purchase != null)
                {
                    if (parsedResponse.Purchase.Chargebacked)
                    {
                        SetLicense(LicenseType.Essentials);
                        ShowErrorMessage("This license key was revoked.");
                        return false;
                    }
                    else
                    {
                        CoreServices.SettingsService.Set(LicenseCodeKey, licenseCode);
                        SetLicense(LicenseType.Pro);
                        if (firstUsage)
                        {
                            ShowSuccessMessage();
                        }
                        
                        return true;
                    }
                }
                else if (!parsedResponse.Success)
                {
                    SetLicense(LicenseType.Essentials);
                    ShowErrorMessage("Invalid license key.");
                    return false;
                }
            }
            catch
            {
                // Deserialization error, treat it as other response errors.
            }

            Logger.LogEventWithParams("Error connecting to gumroad", new Dictionary<string, string> { ["gumroad_response"] = content });
            ShowErrorMessage(
                "Unexpected response from license server. Please, try again later or contact user support.");
            SetLicense(LicenseType.Essentials);
            return false;
        }
        
        SetLicense(LicenseType.Essentials);
        ShowErrorMessage("Error connecting to license server. Please, try again later.");
        return false;
    }

    private async void ShowSuccessMessage()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
            await _dialogService.ShowAlert("Thank you! You are a PRO now!", "License activated"));
    }

    private async void ShowErrorMessage(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () => await _dialogService.ShowAlert(message, "License error"));
    }

    private void SetLicense(LicenseType licenseType)
    {
        AppState.LicenseType = licenseType;
    }

    private bool CheckLicenseCodeFormat(string licenseCode)
    {
        // B6B31466-6BA7431C-96AE5742-0CDC4711
        var regex = new Regex("^[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}$");
        return licenseCode != null && regex.IsMatch(licenseCode);
    }

    private async Task<string> RequestUserLicenseCode()
    {
        return await _dialogService.ShowInputDialogAsync("Please enter license code from your Gumroad purchase",
            "Enter license code");
    }

    private void OpenGumroadPurchasePage()
    {
        _platformStuffService.OpenUrlInBrowser("https://pix2d.gumroad.com/l/FlQVG");
    }

    public GumroadLicenseService(AppState appState, IDialogService dialogService, IBusyController busyController, ISettingsService settingsService, IPlatformStuffService platformStuffService)
    {
        AppState = appState;
        _dialogService = dialogService;
        _busyController = busyController;
        _settingsService = settingsService;
        _platformStuffService = platformStuffService;

        Task.Run(CheckLicenseInSettings);
    }

    private async Task CheckLicenseInSettings()
    {
        if (_settingsService.TryGet<string>(LicenseCodeKey, out var licenseCode))
        {
            await VerifyLicenseCode(licenseCode, false);
        }
    }

    private const string LicenseCodeKey = "gumroad_license";

    public void ToggleIsPro()
    {
        AppState.LicenseType = AppState.IsPro ? LicenseType.Essentials : LicenseType.Pro;
        Logger.Log("$On license changed to " + (AppState.IsPro ? "PRO" : "ESS"));
    }

    private class LicenseInfoResponse
    {
        public bool Success { get; set; }
        public int Uses { get; set; }
        public string Message { get; set; }
        public GumroadPurchase Purchase { get; set; }
    }

    private class GumroadPurchase
    {
        public string SellerId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string Permalink { get; set; }
        public string ProductPermalink { get; set; }
        public string Email { get; set; }
        public int Price { get; set; }
        public int GumroadFee { get; set; }
        public string Currency { get; set; }
        public int Quantity { get; set; }
        public bool DiscoverFeeCharged { get; set; }
        public bool CanContact { get; set; }
        public string Referrer { get; set; }
        public long OrderNumber { get; set; }
        public string SaleId { get; set; }
        public string SaleTimestamp { get; set; }
        public string PurchaserId { get; set; }
        public string SubscriptionId { get; set; }
        public string Variants { get; set; }
        public string LicenseKey { get; set; }
        public bool IsMultiseatLicense { get; set; }
        public string IpCountry { get; set; }
        public string Recurrence { get; set; } 
        public bool IsGiftReceiverPurchase { get; set; }
        public bool Refunded { get; set; }
        public bool Disputed { get; set; }
        public bool DisputeWon { get; set; }
        public string Id { get; set; }
        public bool Chargebacked { get; set; }
        public string SubscriptionEndedAt { get; set; }
        public string SubscriptionCancelledAt { get; set; }
        public string SubscriptionFailedAt { get; set; }
    }
}