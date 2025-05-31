using System.Threading.Tasks;

namespace Pix2d.Abstract.Platform;

public interface IFontService
{
    Task<string[]> GetAvailableFontNamesAsync();
}