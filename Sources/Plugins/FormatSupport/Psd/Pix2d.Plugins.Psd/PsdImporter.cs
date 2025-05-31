using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Plugins.Psd.PsdReader;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Plugins.Psd
{
    public class PsdImporter : IImporter
    {
        public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget targetNode)
        {
            throw new NotImplementedException("Chnage for new IIMpoportServie");

            var file = files.FirstOrDefault();
            if (file == null)
                return;

            using var stream = await file.OpenRead();
            var psdFile = new PsdFile();
            psdFile.Load(stream);

            var size = new SKSize(psdFile.Layers.Max(x => x.Rect.Width), psdFile.Layers.Max(x => x.Rect.Height));

            if (targetNode is SKNode node && node.Parent is IContainerNode artboard)
            {
                ResizeContainer(artboard, size);
            }

            //targetNode.SetSize(size);
            //targetNode.Clear();

            InitLayers(targetNode, psdFile);

            LoadFrameBitmaps(targetNode, psdFile, size);

            //targetNode.SelectedLayerIndex = 0;
            //targetNode.SetFrameIndex(0);
        }

        private void LoadFrameBitmaps(IImportTarget targetNode, PsdFile psdFile, SKSize size)
        {

            var layerIndex = 0;

            foreach (var layer in psdFile.Layers)
            {
                try
                {
                    var bm = ImageDecoder.DecodeImageToSKBitmap(layer);
                    var frameIndex = 0;
                    //if (bm != null)
                        //targetNode.UpdateLayerFrameFromBitmap(frameIndex, layerIndex, bm);
                }
                catch
                {
                    //norm
                }
                layerIndex++;
            }
        }

        private void InitLayers(IImportTarget targetNode, PsdFile psdFile)
        {
            var layersCount = psdFile.Layers.Count();
            foreach (var layer in psdFile.Layers)
            {
                //targetNode.AddLayer(new LayerPropertiesInfo()
                //{
                //    Opacity = layer.Opacity,
                //    BlendMode = GetBlendMode(layer.BlendModeKey)
                //});
            }
        }

        private SKBlendMode GetBlendMode(string? layerBlendMode)
        {
            return SKBlendMode.SrcOver;
        }

        private void ResizeContainer(IContainerNode artboard, SKSize size)
        {
            artboard.Resize(size, 0, 0);
        }
        
    }
}