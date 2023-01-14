using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Pix2d.Abstract
{
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
}
