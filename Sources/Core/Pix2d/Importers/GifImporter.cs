using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Importers
{
    public class GifImporter : IImporter
    {
        public Task<IEnumerable<SKNode>> ImportFromFiles(IEnumerable<IFileContentSource> files)
        {
            throw new System.NotImplementedException();
        }

        public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget targetNode)
        {
            

            using (var stream = await files.First().OpenRead())
            using (var codec = SKCodec.Create(stream))
            {
                var info = codec.Info;
                info = new SKImageInfo(info.Width, info.Height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
                var bitmap = new SKBitmap(info);

                if (targetNode is SKNode node && node.Parent is IContainerNode artboard)
                {
                    artboard.Resize(info.Size, 0, 0);
                }
                targetNode.Clear();
                targetNode.SetSize(info.Size);

                for (int i = 0; i < codec.FrameCount; i++)
                {
                    var opts = new SKCodecOptions(i);

                    codec.GetFrameInfo(i, out var frame);
                    if (codec?.GetPixels(info, bitmap.GetPixels(), opts) == SKCodecResult.Success)
                    {
                        targetNode.AddEmptyFrame(info.Size);
                        targetNode.UpdateLayerFrameFromBitmap(i,0, bitmap);
                    }
                }

            }
        }
    }
}