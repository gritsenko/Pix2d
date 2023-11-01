using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Operations;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services
{
    public class InternalClipboardService : IClipboardService
    {
        public IToolService ToolService { get; }
        public IDrawingService DrawingService { get; }
        public IViewPortService ViewPortService { get; }
        protected SKBitmap SavedBitmap { get; set; }

        public InternalClipboardService(IDrawingService drawingService, IToolService toolService, IViewPortService viewPortService)
        {
            ToolService = toolService;
            DrawingService = drawingService;
            ViewPortService = viewPortService;
        }

        public virtual Task<SKBitmap?> GetImageFromClipboard()
        {
            if (SavedBitmap == null)
                return Task.FromResult<SKBitmap>(null);

            return Task.FromResult(SavedBitmap.Copy());
        }

        public virtual async void TryPaste()
        {
            var img = await GetImageFromClipboard();
            if (img != null)
            {
                new PasteOperation(img, SKPoint.Empty).Invoke();
            }
        }

        public virtual Task<bool> TryCopyNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
        {
            if (nodes == null || !nodes.Any())
            {
                return Task.FromResult(false);
            }
            
            SavedBitmap = nodes.RenderToBitmap(backgroundColor);
            return Task.FromResult(true);
        }

        public virtual async Task<bool> TryCutNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
        {
            if (!await TryCopyNodesAsBitmapAsync(nodes, backgroundColor))
                return false;
            
            DrawingService.DrawingLayer.ClearTarget();
            return true;
        }
    }
}