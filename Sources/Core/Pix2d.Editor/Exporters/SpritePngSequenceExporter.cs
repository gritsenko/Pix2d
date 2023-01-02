using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Exporters
{
    public class SpritePngSequenceExporter
    {
        public string FileNamePrefix { get; set; } = "frame_";

        public async void Export(IWriteDestinationFolder targetFolder, IEnumerable<SKNode> nodesToExport, double scale = 1)
        {
            try
            {
                var nodes = nodesToExport.ToArray();
                List<SKBitmap> frames = new List<SKBitmap>();
                var frameRate = 0.0001f;

                if (nodes.Length > 0 && nodes[0] is IAnimatedNode sprite)
                {
                    frameRate = sprite.FrameRate;
                    var framesCount = sprite.GetFramesCount();

                    var curFrame = sprite.CurrentFrameIndex;
                    for (int index = 0; index < framesCount; index++)
                    {
                        sprite.SetFrameIndex(index);
                        var frame = nodes.RenderToBitmap(SKColor.Empty, scale);
                        frames.Add(frame);
                    }

                    sprite.SetFrameIndex(curFrame);
                    //frames = sprite.RenderFramesToBitmaps();

                }
                else
                {
                    frames.Add(nodes.RenderToBitmap());
                }

                var i = 0;
                foreach (var frame in frames)
                {
                    var file = await targetFolder.GetFileSourceAsync(FileNamePrefix + i.ToString("0000"), "png", true);
                    await file.SaveAsync(frame.ToPngStream());
                    i++;
                }

            }
            catch (Exception e)
            {
                IoC.Get<IDialogService>().Alert("There's nothing to Export!", "Export");
                Logger.Log(e.Message);
            }

        }
    }
}