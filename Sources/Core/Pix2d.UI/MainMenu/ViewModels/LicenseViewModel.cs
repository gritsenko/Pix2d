using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Mvvm;

namespace Pix2d.UI.MainMenu.ViewModels;

public class LicenseViewModel : ViewModelBase
{
    public ILicenseService LicenseService { get; }
    public IDialogService DialogService { get; }

    public string LicenseType
    {
        get => Get<string>();
        set => Set(value);
    }

    public string Price
    {
        get => Get<string>();
        set => Set(value);
    }

    public string UltimatePrice
    {
        get => Get<string>();
        set => Set(value);
    }
    public string OldUltimatePrice
    {
        get => Get<string>();
        set => Set(value);
    }

    public string OldPrice
    {
        get => Get<string>();
        set => Set(value);
    }


    [NotifiesOn(nameof(LicenseType))]
    public bool ShowBuyButton => !LicenseService?.IsPro ?? true;

    public ICommand BuyProCommand => GetCommand(BuyProCommandExecute);
    public ICommand BuyUltimateCommand => GetCommand(BuyUltimateCommandExecute);
        
    public ICommand ToggleProCommand => GetCommand(() =>
    {
#if DEBUG
        LicenseService.ToggleIsPro();
#endif
    });
        
    private async void BuyProCommandExecute()
    {
        try
        {
            Logger.Log("$Pressed buy/restore PRO on Main Menu");
            if (await LicenseService.BuyPro())
            {
                DialogService.Alert("Thank you! Now you are PRO user!", "Success!");
            }
        }
        catch (Exception ex)
        {
            Logger.Log("$Error on buy PRO on Main Menu: " + ex.Message);
            Logger.LogException(ex);
        }
        finally
        {
            Commands.View.HideMainMenuCommand.Execute();
        }

    }
    private async void BuyUltimateCommandExecute()
    {
        try
        {
            Logger.Log("$Pressed pre-order ULTIMATE on Main Menu");
            var result = await DialogService.ShowYesNoDialog(
                "With buying PRO version, you will get ULTIMATE edition for free, on it's release. Do you want to proceed with PRO purchase?",
                "Pre-order Pix2D ULTIMATE", "Yes", "No");

            if (result)
            {
                Logger.Log("$Pressed to buy PRO to ULTIMATE on Main Menu");
                if (await LicenseService.BuyPro())
                {
                    DialogService.Alert("Thank you! Now you are PRO/ULTIMATE user!", "Success!");
                }
            }
            else
            {
                Logger.Log("$Pressed No in pre-order message box PRO on Main Menu");
            }
        }
        catch (Exception ex)
        {
            Logger.Log("$Error on buy PRO(pre-order ULTIMATE) on Main Menu: " + ex.Message);
            Logger.LogException(ex);
        }
        finally
        {
            Commands.View.HideMainMenuCommand.Execute();
        }

    }

    public LicenseViewModel(ILicenseService licenseService, IDialogService dialogService)
    {
        LicenseService = licenseService;
        DialogService = dialogService;
        LicenseService.LicenseChanged += LicenseService_LicenseChanged;
    }

    private void LicenseService_LicenseChanged(object sender, EventArgs e)
    {
        OnLoad();
    }

    private void GenerateOldPrice()
    {
        try
        {
            var price = LicenseService.FormattedPrice;
            var match = Regex.Match(price, @"\d+(,\d+)*(\.\d+)");

            double val = 0;
            if (match.Success && double.TryParse(match.Value, CultureInfo.InvariantCulture, out val))
            {
                var pattern = price.Replace(match.Value, "%price");
                OldPrice = price;
                var oldPrice = Math.Round(val * 2.5 / 10) * 10;
                var ultimatePrice = Math.Round(oldPrice * 1.5);
                var oldUltimatePrice = Math.Round(ultimatePrice * 2.5 / 10) * 10;

                OldPrice = pattern.Replace("%price", oldPrice.ToString("#.00"));
                UltimatePrice = pattern.Replace("%price", ultimatePrice.ToString("#.00"));
                OldUltimatePrice = pattern.Replace("%price", oldUltimatePrice.ToString("#.00"));
            }
        }
        catch (Exception exception)
        {
            Logger.LogException(exception);
        }
    }

    public void OnLoad()
    {
        Logger.Log("$License view opened");
        var isPro = LicenseService.IsPro;
        LicenseType = isPro ? "PRO" : "ESSENTIALS";
        Price = LicenseService.FormattedPrice;

        GenerateOldPrice();
    }
}