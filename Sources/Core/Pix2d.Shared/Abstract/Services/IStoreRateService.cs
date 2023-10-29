using System.Threading.Tasks;

namespace Pix2d.Abstract.Services;

public interface IStoreRateService
{
    Task<bool> RateApp();
}