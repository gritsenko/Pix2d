#nullable enable
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.PngFormat.Exporters;

public class PngImageExporter(IFileService fileService) : IStreamExporter, IFilePickerExporter
{
    public string? Title => "PNG image";

    public Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        return ExportToFileAsync(nodes, scale);
    }

    public string[] SupportedExtensions => new[] { ".png" };
    public string MimeType => "image/png";

    public Task<Stream> ExportToStreamAsync(IEnumerable<SKNode> nodesToExport, double scale = 1)
    {
        var skBitmap = nodesToExport.RenderToBitmap(SKColor.Empty, scale);
        return Task.FromResult(skBitmap.ToPngStream());
    }

    public async Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        var result =
            await fileService.SaveStreamToFileWithDialogAsync(() => ExportToStreamAsync(nodes, scale), [".png"],
                "project");

        if (!result)
            throw new OperationCanceledException("Selection file canceled");
    }
}