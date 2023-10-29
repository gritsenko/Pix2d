using System.Threading.Tasks;

namespace Pix2d.Abstract.Services;

public interface ILicenseService
{
    string GetFormattedPrice { get; }

    Task<bool> BuyPro();
}