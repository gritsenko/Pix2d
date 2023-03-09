using Pix2d.Abstract.Platform;
using System.Threading.Tasks;

namespace Pix2d.Services
{
    public class AndroidFontService : IFontService
    {
        public async Task<string[]> GetAvailableFontNamesAsync()
        {
            string[] fonts = new[] { "normal", "serif", "monospace" };
            return fonts;
        }
    }
}