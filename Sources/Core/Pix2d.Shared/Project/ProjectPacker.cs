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
        
        if (scene.Nodes.FirstOrDefault() is Pix2dSprite sprite)
        {
            var preview = GetPreview(sprite);
            await using var previewStream = zip.CreateEntry("__project_thumbnail.jpg", CompressionLevel.NoCompression).Open();
            preview.Encode(previewStream, SKEncodedImageFormat.Jpeg, 75);
        }
    }

    private static SKBitmap GetPreview(Pix2dSprite sprite)
    {
        var size = sprite.Size;
        const float previewSize = 128;
        var scale = size.GetAspect() > 1 ? previewSize / size.Width : previewSize / size.Height;
        var bitmapSize = new SKSize(size.Width * scale, size.Height * scale);
        var bitmap = new SKBitmap((int)bitmapSize.Width, (int)bitmapSize.Height, Pix2DAppSettings.ColorType,
            SKAlphaType.Premul);
        sprite.RenderFramePreview(sprite.CurrentFrameIndex, ref bitmap, scale);

        return bitmap;
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