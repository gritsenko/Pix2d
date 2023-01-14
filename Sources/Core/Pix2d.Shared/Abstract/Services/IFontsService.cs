using System.Collections.Generic;

namespace Pix2d.Abstract.Services
{
    public interface IFontsService
    {
        ICollection<string> SystemFontNames { get; set; }

        bool CheckIsFontInstalled(string fontName);

        void FindFont(string fontName);
    }
}
