using Pix2d.Abstract.Platform;

namespace Pix2d.Services;

public class AvaloniaFontService : IFontService
{
    public Task<string[]> GetAvailableFontNamesAsync()
    {
        var fm = Avalonia.Media.FontManager.Current;
        var fonts = fm.SystemFonts;
        return Task.FromResult(fonts.Select(x => x.Name).ToArray());
    }
}
