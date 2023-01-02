using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Abstract.Platform
{
    public interface IClipboardService
    {
        Task<SKBitmap> GetImageFromClipboard();
        void TryPaste();
        Task<bool> TryCopyNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor);
        Task<bool> TryCutNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor);
    }
}