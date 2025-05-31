using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Abstract.Platform;
using Pix2d.Plugins.ImageFormats.GifFormat.Exporters;
using Pix2d.Plugins.ImageFormats.GifFormat.Importers;

namespace Pix2d.Plugins.ImageFormats.GifFormat;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(GifFormatPlugin))]
public class GifFormatPlugin(IExportService exportService, IImportService importService, IFileService fileService) : IPix2dPlugin
{
    public void Initialize()
    {
        exportService.RegisterExporter<GifImageExporter>("GIF Animation", () => new GifImageExporter(fileService));

        importService.RegisterImporter<GifImporter>(".gif", () => new GifImporter());
    }
}