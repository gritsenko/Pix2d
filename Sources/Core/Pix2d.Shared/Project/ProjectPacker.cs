using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
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

        const CompressionLevel compressionLevel = CompressionLevel.Fastest;

        using var serializer = new NodeSerializer();
        var sceneJson = serializer.Serialize(scene);

        using var outputFileStream = await file.OpenWriteAsync();
        using var zip = new ZipArchive(outputFileStream, ZipArchiveMode.Create, true, ZipEncoding);
        var projectZipEntry = zip.CreateEntry("project.json", compressionLevel);

        using (var projectZipStream = projectZipEntry.Open())
        using (var streamWriter = new StreamWriter(projectZipStream))
            await streamWriter.WriteAsync(sceneJson);

        foreach (var (key, bitmap) in serializer.GetDataEntries().Select(x => (key: x.Key, bitmap: x.Value)))
            using (var entryStream = zip.CreateEntry(key, compressionLevel).Open())
                bitmap.Encode(entryStream, SKEncodedImageFormat.Png, 100);

        await outputFileStream.FlushAsync();
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