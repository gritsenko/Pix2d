using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;

namespace Pix2d.Abstract
{
    public interface IImporter
    {
        Task<IEnumerable<SKNode>> ImportFromFiles(IEnumerable<IFileContentSource> files);
        Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget targetNode);
    }

    public interface IImportTarget
    {
        void AddEmptyFrame(SKSize size = default);

        void UpdateLayerFrameFromBitmap(int frameIndex, int layerIndex, SKBitmap sourceBitmap);
        void AddLayer(LayerPropertiesInfo layerPropertiesInfo, SKSize size = default);

        /// <summary>
        /// removes all frames and layers
        /// </summary>
        void Clear();
        void SetSize(SKSize size);
        void SetFrameIndex(int index);

        int SelectedLayerIndex { get; set; }
    }

    public class LayerPropertiesInfo
    {
        public float Opacity { get; set; } = 1;

        public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;
    }
}