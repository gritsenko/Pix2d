using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Platform;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Exporters
{

    public abstract class SKNodeExporterBase : IExporter
    {
        protected abstract Stream EncodeFrames(IEnumerable<SKBitmap> frames, float frameRate, double scale);

        public Stream Export(IEnumerable<SKNode> nodesToExport, double scale = 1)
        {
            try
            {
                var frameRate = 0.0001f;
                var nodes = nodesToExport.ToArray();
                List<SKBitmap> frames = new List<SKBitmap>();

                if (nodes.Length > 0 && nodes[0] is IAnimatedNode sprite)
                {
                    frameRate = sprite.FrameRate;
                    var framesCount = sprite.GetFramesCount();

                    var curFrame = sprite.CurrentFrameIndex;
                    for (int index = 0; index < framesCount; index++)
                    {
                        sprite.SetFrameIndex(index);
                        var frame = nodes.RenderToBitmap(SKColor.Empty);
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
                return stream;

            }
            catch (Exception e)
            {
                IoC.Get<IDialogService>().Alert("There's nothing to Export!", "Export");
                Logger.Log(e.Message);
            }

            return null;
        }
    }
}