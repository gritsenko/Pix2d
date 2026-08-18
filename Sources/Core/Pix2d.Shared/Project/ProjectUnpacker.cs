using System.IO.Compression;
using System.Text;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Serialization;
using SkiaSharp;


namespace Pix2d.Project;

public class ProjectUnpacker
{
    private static readonly Encoding ZipEncoding = Encoding.UTF8;

    public static async Task<SKNode?> LoadProjectScene(IFileContentSource file)
    {
        if (!file.Exists)
            throw new Exception($"File {file.Path} does not exists!");

        await using var fileStream = await file.OpenRead();
        if (!fileStream.CanRead || fileStream.Length < 1 || fileStream.Position < 0)
            throw new Exception($"Can't read file stream {file.Path} with size of {fileStream.Length}! [CanRead: {fileStream.CanRead}, Stream pos: {fileStream.Position}]");

        return await LoadProjectSceneFromStream(fileStream, file.Path);
    }

    /// <summary>
    /// Reads a <c>.pix2d</c> archive from an already-open stream. Split out from
    /// <see cref="LoadProjectScene(IFileContentSource)"/> so tools that hold a raw stream (e.g. the
    /// format-corpus test harness) can exercise the exact production load path without a platform
    /// file-source implementation. The scene is deserialized through <see cref="ProjectFormat"/> so
    /// the format version recorded in <c>manifest.json</c> drives any migration.
    /// </summary>
    public static async Task<SKNode?> LoadProjectSceneFromStream(Stream fileStream, string pathForErrors)
    {
        try
        {
            using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read, true, ZipEncoding);
            var images = new Dictionary<string, SKBitmap>();

            var imageZipEntries = zip.Entries.Where(x => x.Name.EndsWith(".png")).ToArray();

            foreach (var imageZipEntry in imageZipEntries)
            {
                await using var imageEntryStream = imageZipEntry.Open();
                using var ms = new MemoryStream();
                //if we're reading from zip entry image isn't fully loaded
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

            var projectEntry = zip.GetEntry("project.json");
            if (projectEntry == null)
                return null;

            await using var projectStream = projectEntry.Open();
            using var streamReader = new StreamReader(projectStream);
            var sceneJson = await streamReader.ReadToEndAsync();

            var formatVersion = await ReadFormatVersionAsync(zip);
            var scene = ProjectFormat.DeserializeScene(sceneJson, formatVersion, images);

            return scene;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Can't read file {pathForErrors} with size of {(fileStream.CanSeek ? fileStream.Length : -1)}!  \nException while unpack: {ex.Message}",
                ex);

        }
    }

    /// <summary>
    /// Reads the format version from the archive's <c>manifest.json</c>. Archives written before the
    /// manifest existed have none — those are the baseline version.
    /// </summary>
    private static async Task<int> ReadFormatVersionAsync(ZipArchive zip)
    {
        var manifestEntry = zip.GetEntry("manifest.json");
        if (manifestEntry == null)
            return ProjectFormat.BaselineVersion;

        try
        {
            await using var manifestStream = manifestEntry.Open();
            using var reader = new StreamReader(manifestStream);
            var json = await reader.ReadToEndAsync();
            var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectManifest>(json);
            return manifest?.FormatVersion ?? ProjectFormat.BaselineVersion;
        }
        catch
        {
            return ProjectFormat.BaselineVersion;
        }
    }

    public static async Task<SKBitmap?> LoadPreview(IFileContentSource file)
    {
        if (!file.Exists)
            return null;

        using var fileStream = await file.OpenRead();
        if (!fileStream.CanRead || fileStream.Length < 1 || fileStream.Position < 0)
            return null;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(fileStream, ZipArchiveMode.Read, true, ZipEncoding);
        }
        catch (InvalidDataException)
        {
            // Not a readable archive — a truncated or otherwise corrupt .pix2d. The recent-projects gallery
            // asks every entry for a thumbnail on a background task, so throwing here took the app down
            // (appstat, fatal: "End of Central Directory record could not be found"). A missing thumbnail is
            // the same outcome as every other unreadable case this method already returns null for; opening
            // the file is where the user is told it is broken.
            return null;
        }

        using var _ = zip;

        var previewEntry = zip.Entries.FirstOrDefault(x => x.Name == "__project_thumbnail.jpg");
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