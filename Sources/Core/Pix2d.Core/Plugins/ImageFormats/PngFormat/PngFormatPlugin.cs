using Pix2d.Abstract.Platform;
using Pix2d.Plugins.ImageFormats.PngFormat.Importers;
using Pix2d.Plugins.PngFormat.Exporters;

namespace Pix2d.Plugins.ImageFormats.PngFormat;

public class PngFormatPlugin(
    IExportService exportService,
    IImportService importService,
    IFileService fileService,
    IDialogService dialogService) : IPix2dPlugin
{
    public void Initialize()
    {
        exportService.RegisterExporter<PngImageExporter>("Single Png image", () => new PngImageExporter(fileService));
        exportService.RegisterExporter<SpritePngSequenceExporter>("Png sequence",
            () => new SpritePngSequenceExporter(fileService, dialogService));
        exportService.RegisterExporter<SpritesheetImageExporter>("Sprite-sheet (png)",
            () => new SpritesheetImageExporter(fileService));

        importService.RegisterImporter<PngFileImporter>(".png", () => new PngFileImporter());
    }
}