using System.Threading.Tasks;
using Pix2d.Abstract.Platform;

namespace Pix2d.iOS.Services;

public class IosFontService : IFontService
{
    public async Task<string[]> GetAvailableFontNamesAsync()
    {
        string[] fonts = new[] {"normal", "serif", "monospace"};
        return fonts;
    }
}