using Pix2d.Abstract.Platform;

namespace Pix2d.BlazorWasm.Services
{
    public class BlazorFontService : IFontService
    {
        public Task<string[]> GetAvailableFontNamesAsync()
        {
            return Task.FromResult(new[] {"Arial"});
        }
    }
}