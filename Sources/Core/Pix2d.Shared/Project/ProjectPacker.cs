using System.IO.Compression;
using System.Text;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Serialization;
using SkiaSharp;


namespace Pix2d.Project;

public class ProjectPacker
{
    private static readonly Encoding ZipEncoding = Encoding.UTF8;

    public static async Task WriteProjectAsync(IFileContentSource file, SKNode scene)
    {
        using var serializer = new NodeSerializer();
        var sceneJson = serializer.Serialize(scene);

        await using var outputFileStream = await file.OpenWriteAsync();
        using var zip = new ZipArchive(outputFileStream, ZipArchiveMode.Create, true, ZipEncoding);
        var projectZipEntry = zip.CreateEntry("project.json", CompressionLevel.Fastest);

        await using (var projectZipStream = projectZipEntry.Open())
        await using (var streamWriter = new StreamWriter(projectZipStream))
            await streamWriter.WriteAsync(sceneJson);

        foreach (var (key, bitmap) in serializer.GetDataEntries().Select(x => (key: x.Key, bitmap: x.Value)))
            await using (var entryStream = zip.CreateEntry(key, CompressionLevel.NoCompression).Open())
                bitmap.Encode(entryStream, SKEncodedImageFormat.Png, 100);
        
        // Composite thumbnail: render every artboard at its scene position into one preview, so a project
        // with several artboards is recognisable in the file browser / recent list (not just the first one).
        var sprites = scene.Nodes.OfType<Pix2dSprite>().ToList();
        if (sprites.Count > 0)
        {
            using var preview = GetCompositePreview(sprites);
            await using var previewStream = zip.CreateEntry("__project_thumbnail.jpg", CompressionLevel.NoCompression).Open();
            preview.Encode(previewStream, SKEncodedImageFormat.Jpeg, 75);
        }
    }

    private static SKBitmap GetCompositePreview(IReadOnlyList<Pix2dSprite> sprites)
    {
        const float previewSize = 128;
        var bounds = sprites.GetBounds();
        var longest = Math.Max(bounds.Width, bounds.Height);
        var scale = longest > 0 ? previewSize / longest : 1f;
        // RenderToBitmap frames the union of all sprite bounds with RenderAdorners off, so the active-artboard
        // highlight border and grid never leak into the saved thumbnail.
        return sprites.RenderToBitmap(SKColor.Empty, scale);
    }

    public static async Task WriteProjectAsync(IWriteDestinationFolder folder, SKNode scene)
    {
        if (folder == null)
            return;

        await folder.ClearFolderAsync();

        using var serializer = new NodeSerializer();
        var sceneJson = serializer.Serialize(scene);
        var projectFile = await folder.GetFileSourceAsync("project", "pix2d.json", true);
        await projectFile.SaveAsync(sceneJson);
        //saving images
        foreach (var entry in serializer.GetDataEntries())
        {
            var entryFile = await GetResourceFileAsync(folder, entry.Key, "png");
            using var dataStream = entry.Value.ToPngStream();
            await entryFile.SaveAsync(dataStream);
        }
    }

    public static async Task<IFileContentSource> GetResourceFileAsync(IWriteDestinationFolder projectFolder, string key, string extension)
    {
        var resFolder = await projectFolder.GetSubfolderAsync("Resources");

        var entryFile = await resFolder.GetFileSourceAsync(key.Replace(extension, "").TrimEnd('.'), extension, true);
        return entryFile;
    }
}