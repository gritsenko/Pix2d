using System;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;

namespace Pix2d.Editor.Desktop.Services;

public class AvaloniaFontService : IFontService
{
    public Task<string[]> GetAvailableFontNamesAsync()
    {
        var fm = Avalonia.Media.FontManager.Current;
        var fonts = fm.GetInstalledFontFamilyNames();

        return Task.FromResult(fonts.ToArray());
    }
}
