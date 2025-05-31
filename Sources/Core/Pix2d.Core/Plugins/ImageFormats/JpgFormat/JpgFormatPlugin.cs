using Pix2d.Abstract.Services;
using Pix2d.Plugins.ImageFormats.JpgFormat.Importers;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.ImageFormats.JpgFormat;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(JpgFormatPlugin))]
public class JpgFormatPlugin(IImportService importService) : IPix2dPlugin
{
    public void Initialize()
    {
        importService.RegisterImporter<JpgFileImporter>(".jpg", () => new JpgFileImporter());
        importService.RegisterImporter<JpgFileImporter>(".jpeg", () => new JpgFileImporter());
    }
}