using System.Threading.Tasks;
using Pix2d.Abstract.Platform;

namespace Pix2d.Browser.Services;

public class BrowserFontService : IFontService
{
    public Task<string[]> GetAvailableFontNamesAsync()
    {
            return Task.FromResult(new[] {"Arial"});
        }
}