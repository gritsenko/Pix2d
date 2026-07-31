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

    public string[] SupportedExtensions => new[] { ".png" };
    public string MimeType => "image/png";

    public Task<Stream> ExportToStreamAsync(IEnumerable<SKNode> nodesToExport, double scale = 1)
    {
        var skBitmap = nodesToExport.RenderToBitmap(SKColor.Empty, scale);
        return Task.FromResult(skBitmap.ToPngStream());
    }

    public async Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1, string? defaultFileName = null)
    {
        // "export" context, like every other exporter — the old "project" key made a PNG export reopen the
        // folder the .pix2d was last saved to instead of the one the user exports images into.
        var result =
            await fileService.SaveStreamToFileWithDialogAsync(() => ExportToStreamAsync(nodes, scale), [".png"],
                "export", defaultFileName);

        if (!result)
            throw new OperationCanceledException("Selection file canceled");
    }
}