using System;
using System.Threading.Tasks;
using Pix2d.Primitives;

namespace Pix2d.Abstract.Services;

public interface ILicenseService
{
    event EventHandler LicenseChanged;
    
    LicenseType License { get; }

    string FormattedPrice { get; }

    bool AllowBuyPro { get; }

    bool IsPro { get; }

    Task<bool> BuyPro();

    void ToggleIsPro();
    Task<bool> RateApp();
}