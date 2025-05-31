#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.NodeTypes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Exporters;

public abstract class SKNodeExporterBase : IStreamExporter
{
    protected abstract Stream EncodeFrames(IEnumerable<SKBitmap> frames, float frameRate, double scale);

    public virtual string? Title => GetType().Name;
    public abstract string[] SupportedExtensions { get; }
    public abstract string MimeType { get; }

    public virtual Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> ExportToStreamAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        var frameRate = 0.0001f;
        var nodesToExport = nodes.ToArray();
        var frames = new List<SKBitmap>();

        if (nodesToExport.Length > 0 && nodesToExport[0] is IAnimatedNode sprite)
        {
            frameRate = sprite.FrameRate;
            var framesCount = sprite.GetFramesCount();

            var curFrame = sprite.CurrentFrameIndex;
            for (int index = 0; index < framesCount; index++)
            {
                sprite.SetFrameIndex(index);
                var frame = nodesToExport.RenderToBitmap(SKColor.Empty, scale);
                frames.Add(frame);
            }

            sprite.SetFrameIndex(curFrame);
            //frames = sprite.RenderFramesToBitmaps();
        }
        else
        {
            frames.Add(nodes.RenderToBitmap());
        }

        var stream = EncodeFrames(frames, frameRate, scale);
        return Task.FromResult(stream);
    }
}