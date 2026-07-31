#nullable enable
using Pix2d.Abstract.Export;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.PngFormat.Exporters;

/// <summary>
/// One PNG per animation frame. Deliberately not an <see cref="IFilePickerExporter"/> — its output is a set
/// of files, never one, so <c>IExportService</c> always routes it through a folder destination.
/// </summary>
public class SpritePngSequenceExporter : IBatchExporter
{
    public string FileNamePrefix { get; set; } = "frame_";

    public string? Title => "Frames to PNG sequence";

    public string[] SupportedExtensions => new[] { ".png" };
    public string MimeType => "image/png";

    /// <summary>The frame file names come from <see cref="FileNamePrefix"/> + index, not from the base name,
    /// so several artboards written into one folder would overwrite each other — each needs its own.</summary>
    public bool NeedsOwnFolderPerItem => true;

    public async Task ExportToFolderAsync(IEnumerable<SKNode> nodes, double scale, IWriteDestinationFolder folder,
        string baseName)
    {
        var nodesToExport = nodes.ToArray();
        var index = 0;

        foreach (var frame in RenderFrames(nodesToExport, scale))
        {
            using (frame)
            {
                // Overwrite: the caller has already confirmed the destination, and stable names are what make
                // a re-export drop into the same pipeline instead of piling up "frame_0000_1.png" siblings.
                var file = await folder.GetFileSourceAsync(FileNamePrefix + index.ToString("0000"), "png", true);
                await file.SaveAsync(frame.ToPngStream());
            }

            index++;
        }
    }

    /// <summary>Renders lazily, one frame at a time, so a long animation at a high scale never holds every
    /// frame bitmap in memory at once (the old version built the whole list up front).</summary>
    private static IEnumerable<SKBitmap> RenderFrames(SKNode[] nodes, double scale)
    {
        if (nodes.Length == 0 || nodes[0] is not IAnimatedNode sprite)
        {
            yield return nodes.RenderToBitmap(SKColor.Empty, scale);
            yield break;
        }

        var framesCount = sprite.GetFramesCount();
        var currentFrame = sprite.CurrentFrameIndex;
        try
        {
            for (var i = 0; i < framesCount; i++)
            {
                sprite.SetFrameIndex(i);
                yield return nodes.RenderToBitmap(SKColor.Empty, scale);
            }
        }
        finally
        {
            sprite.SetFrameIndex(currentFrame);
        }
    }
}
