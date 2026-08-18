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

    /// <summary>
    /// Writes the project archive to <paramref name="file"/> without ever leaving the destination
    /// half-written.
    ///
    /// <para>The archive used to be streamed straight into the destination, which truncated the user's
    /// existing <c>.pix2d</c> the moment the save began and left it incomplete for as long as
    /// serialization and PNG encoding took. A disk-full (appstat: <c>Espace insuffisant sur le disque</c>),
    /// an unplugged drive or a killed process then left an unopenable file where the project used to be
    /// (appstat: <c>End of Central Directory record could not be found</c>). The destination must
    /// therefore not be touched until the whole archive exists somewhere else first.</para>
    ///
    /// <para>Which is what <see cref="IFileContentSource.OpenStagedWriteAsync"/> provides, so there is one
    /// code path here and the platform difference stays inside the file source: a real path stages beside
    /// the destination and the archive streams to disk entry by entry with nothing buffered, while a SAF /
    /// browser handle buffers because it has no rename primitive.</para>
    /// </summary>
    public static async Task WriteProjectAsync(IFileContentSource file, SKNode scene)
    {
        using var serializer = new NodeSerializer();
        var sceneJson = serializer.Serialize(scene);

        await using var staged = await file.OpenStagedWriteAsync();

        // The ZipArchive must be disposed before the commit — that is what writes the central directory,
        // and an archive without one is precisely the corrupt file this guards against.
        using (var zip = new ZipArchive(staged.Stream, ZipArchiveMode.Create, true, ZipEncoding))
            await WriteEntriesAsync(zip, serializer, sceneJson, scene);

        await staged.CommitAsync();
    }

    private static async Task WriteEntriesAsync(ZipArchive zip, NodeSerializer serializer, string sceneJson, SKNode scene)
    {

        // Format version anchor (H1.2): lets the unpacker migrate older documents on open. Written
        // first so it is cheap to read without inflating the whole archive.
        var manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(
            new ProjectManifest { FormatVersion = ProjectFormat.CurrentVersion });
        var manifestZipEntry = zip.CreateEntry("manifest.json", CompressionLevel.Fastest);
        await using (var manifestZipStream = manifestZipEntry.Open())
        await using (var manifestWriter = new StreamWriter(manifestZipStream))
            await manifestWriter.WriteAsync(manifestJson);

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
}
