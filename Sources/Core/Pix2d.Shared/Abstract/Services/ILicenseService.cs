using System;
using System.Threading.Tasks;

namespace Pix2d.Abstract.Services;

public interface ILicenseService
{
    event EventHandler LicenseChanged;

    string FormattedPrice { get; }

    bool AllowBuyPro { get; }

    bool IsPro { get; }

    Task<bool> BuyPro();

    void ToggleIsPro();
    Task<bool> RateApp();
}
