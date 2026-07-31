#nullable enable
using System.IO;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.Export.Sheet;
using Pix2d.Export.Sheet.Metadata;
using SkiaNodes;
using SkiaNodes.Extensions;

namespace Pix2d.Plugins.PngFormat.Exporters;

/// <summary>
/// Sprite-sheet export v2. Renders the active sprite's frames, packs them (grid or tight, optional trim
/// / power-of-two), writes the sheet PNG, and — unless the metadata format is "none" — writes an
/// engine-consumable metadata sidecar (Aseprite-compatible JSON) next to it. The packing + metadata
/// engine lives in <see cref="SpriteSheetBuilder"/> (Pix2d.Shared), so the headless CLI shares it.
/// </summary>
public class SpriteSheetExporter(IFileService fileService, IPlatformStuffService platformStuff)
    : IStreamExporter, IFilePickerExporter, IBatchExporter
{
    public string? Title => "Sprite sheet (PNG + JSON)";

    // Options mutated by SpriteSheetExportSettingsView.
    public SheetPackMode PackMode { get; set; } = SheetPackMode.Grid;
    public int MaxColumns { get; set; } = 4;
    public int Padding { get; set; } = 0;
    public bool Trim { get; set; }
    public bool PowerOfTwo { get; set; }

    /// <summary>Metadata sidecar format id (<see cref="SheetMetadataEmitters"/>), or "none".</summary>
    public string MetadataFormat { get; set; } = "aseprite";

    public string[] SupportedExtensions => [".png"];
    public string MimeType => "image/png";

    /// <summary>Sheet PNG + sidecar are both named after the base name, so a batch can share one folder.</summary>
    public bool NeedsOwnFolderPerItem => false;

    public Task<Stream> ExportToStreamAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        // Share / single-stream consumers get the sheet image only (metadata isn't shareable).
        using var sheet = BuildSheet(nodes, scale, "spritesheet.png", "spritesheet");
        return Task.FromResult(sheet.Image.ToPngStream());
    }

    public async Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1, string? defaultFileName = null)
    {
        var pickResult = await fileService.GetFileToSaveWithDialogAsync([".png"], "export",
            string.IsNullOrWhiteSpace(defaultFileName) ? "spritesheet" : defaultFileName);

        var saved = await pickResult.MatchAsync(async pngFile =>
        {
            var imageName = Path.GetFileName(pngFile.Path);
            var spriteName = Path.GetFileNameWithoutExtension(pngFile.Path);
            if (string.IsNullOrWhiteSpace(imageName)) imageName = "spritesheet.png";
            if (string.IsNullOrWhiteSpace(spriteName)) spriteName = "spritesheet";

            using var sheet = BuildSheet(nodes, scale, imageName, spriteName);

            await using (var png = sheet.Image.ToPngStream())
            await using (var outStream = await pngFile.OpenWriteAsync())
            {
                await png.CopyToAsync(outStream);
                await outStream.FlushAsync();
            }

            var emitter = SheetMetadataEmitters.TryGet(MetadataFormat);
            if (emitter != null)
            {
                var json = emitter.Emit(sheet, new SheetMetadataOptions { AppVersion = platformStuff.GetAppVersion() });
                await WriteSidecarAsync(pngFile, emitter.FileExtension, json);
            }

            return true;
        }, _ => Task.FromResult(false));

        if (!saved)
            throw new OperationCanceledException("Sprite sheet export canceled");
    }

    /// <summary>
    /// Batch destination: the caller already picked (and confirmed) the folder, so write
    /// <c>{baseName}.png</c> + <c>{baseName}.{sidecar}</c> straight into it — no second prompt, and the
    /// sidecar always lands beside its image on every head (the file-picker path has to fall back to a
    /// second dialog on mobile/web because a picked file has no usable sibling path there).
    /// </summary>
    public async Task ExportToFolderAsync(IEnumerable<SKNode> nodes, double scale, IWriteDestinationFolder folder,
        string baseName)
    {
        using var sheet = BuildSheet(nodes, scale, baseName + ".png", baseName);

        var pngFile = await folder.GetFileSourceAsync(baseName, "png", overwrite: true);
        await using (var png = sheet.Image.ToPngStream())
        {
            await pngFile.SaveAsync(png);
        }

        var emitter = SheetMetadataEmitters.TryGet(MetadataFormat);
        if (emitter == null)
            return;

        var json = emitter.Emit(sheet, new SheetMetadataOptions { AppVersion = platformStuff.GetAppVersion() });
        var sidecar = await folder.GetFileSourceAsync(baseName, emitter.FileExtension, overwrite: true);
        await sidecar.SaveAsync(json);
    }

    private PackedSheet BuildSheet(IEnumerable<SKNode> nodes, double scale, string imageFileName, string spriteName)
    {
        var sprite = nodes.OfType<Pix2dSprite>().FirstOrDefault()
                     ?? throw new InvalidOperationException("Sprite sheet export requires a sprite artboard.");

        var options = new SpriteSheetOptions
        {
            PackMode = PackMode,
            MaxColumns = MaxColumns,
            Padding = Padding,
            Trim = Trim,
            PowerOfTwo = PowerOfTwo,
            SpriteName = spriteName,
            ImageFileName = imageFileName
        };

        return SpriteSheetBuilder.Build(sprite, scale, options);
    }

    private async Task WriteSidecarAsync(IFileContentSource pngFile, string extension, string text)
    {
        var path = pngFile.Path;
        var dir = string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);

        // Desktop: the picked file resolves to a real filesystem path, so write the sidecar right next
        // to the image (the universal engine-importer convention).
        if (!string.IsNullOrEmpty(path) && Path.IsPathRooted(path) && !string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            var sidecarPath = Path.ChangeExtension(path, extension);
            var sidecar = await fileService.GetFileContentSourceAsync(sidecarPath);
            await sidecar.SaveAsync(text);
        }
        else
        {
            // Mobile / web: no usable sibling path (SAF/browser download) — ask once more for the sidecar.
            var suggested = string.IsNullOrEmpty(path) ? "spritesheet" : Path.GetFileNameWithoutExtension(path);
            await fileService.SaveTextToFileWithDialogAsync(text, [extension], "export", suggested);
        }
    }
}
