using System.Collections.Generic;
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

public class ProjectUnpacker
{
    private static readonly Encoding ZipEncoding = Encoding.UTF8;

    public static async Task<SKNode> LoadProjectScene(IFileContentSource file)
    {
        if (!file.Exists)
            return null;

        using var fileStream = await file.OpenRead();
        if (!fileStream.CanRead || fileStream.Length < 1 || fileStream.Position < 0) 
            return null;

        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read, true, ZipEncoding);
        var images = new Dictionary<string, SKBitmap>();

        var imageZipEntries = zip.Entries.Where(x => x.Name.EndsWith(".png")).ToArray();

        foreach (var imageZipEntry in imageZipEntries)
        {
            using var imageEntryStream = imageZipEntry.Open();
            using var ms = new MemoryStream();
            //если читаем напрямую из zip entry то картинка не догружается
            await imageEntryStream.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);

            using var codec = SKCodec.Create(ms);
            if (codec == null)
                return null;

            var info = codec.Info;
            info.ColorType = Pix2DAppSettings.ColorType;
            info.AlphaType = SKAlphaType.Premul;

            var srcBm = SKBitmap.Decode(codec, info);
            images.Add(imageZipEntry.Name, srcBm);
        }

        using var projectStream = zip.GetEntry("project.json").Open();
        using var streamReader = new StreamReader(projectStream);
        var sceneJson = await streamReader.ReadToEndAsync();

        var scene = NodeSerializer.Deserialize<SKNode>(sceneJson, images);

        return scene;
    }

    public static async Task<SKNode> LoadProjectFolderAsync(IWriteDestinationFolder folder)
    {
        var files = await folder.GetFilesAsync("Resources");
        var images = new Dictionary<string, SKBitmap>();

        foreach (var file in files)
        {
            using (var data = await file.OpenRead())
            {
                data.Seek(0, SeekOrigin.Begin);
                var bm = data.ToSKBitmap();
                images.Add(Path.GetFileName(file.Path), bm);
            }
        }

        var projectFile = await folder.GetFileSourceAsync("project", "pix2d.json", true);
        using (var reader = new StreamReader(await projectFile.OpenRead()))
        {
            var sceneJson = reader.ReadToEnd();
            var scene = NodeSerializer.Deserialize<SKNode>(sceneJson, images);

            return scene;
        }
    }

    public static async Task<IFileContentSource> GetResourceFileAsync(IWriteDestinationFolder projectFolder, string key, string extension)
    {
        var resFolder = await projectFolder.GetSubfolderAsync("Resources");

        var entryFile = await resFolder.GetFileSourceAsync(key, extension, false);
        return entryFile;
    }

    public static async Task<SKBitmap> LoadPreview(IFileContentSource file)
    {
        if (!file.Exists)
            return null;

        using var fileStream = await file.OpenRead();
        if (!fileStream.CanRead || fileStream.Length < 1 || fileStream.Position < 0) 
            return null;

        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read, true, ZipEncoding);

        // Use pngx extension to differentiate from sprite images. Probably need to fix this later.
        var previewEntry = zip.Entries.FirstOrDefault(x => x.Name == "preview.pngx");
        if (previewEntry == null)
        {
            return null;
        }

        using var imageEntryStream = previewEntry.Open();
        using var ms = new MemoryStream();
        //если читаем напрямую из zip entry то картинка не догружается
        await imageEntryStream.CopyToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);

        using var codec = SKCodec.Create(ms);
        if (codec == null)
            return null;

        var info = codec.Info;
        info.ColorType = Pix2DAppSettings.ColorType;
        info.AlphaType = SKAlphaType.Premul;

        var srcBm = SKBitmap.Decode(codec, info);

        return srcBm;
    }
}