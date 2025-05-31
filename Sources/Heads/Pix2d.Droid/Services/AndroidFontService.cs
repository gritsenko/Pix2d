using System.Threading.Tasks;
using Pix2d.Abstract.Platform;

namespace Pix2d.Droid.Services;

public class AndroidFontService : IFontService
{
    public async Task<string[]> GetAvailableFontNamesAsync()
    {
            string[] fonts = new[] { "normal", "serif", "monospace" };
            return fonts;
        }
}