using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
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

        public virtual Task<SKBitmap> GetImageFromClipboard()
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
                DrawingService.PasteBitmap(img, SKPoint.Empty);

                ViewPortService.Refresh();
            }
        }

        public virtual Task<bool> TryCopyNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
        {
            if (!nodes.Any())
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
            
            var drawingLayer = DrawingService.DrawingLayer;
            if (drawingLayer.HasSelection)
            {
                var selectionLayer = drawingLayer.GetSelectionLayer();

                if (!(selectionLayer is BitmapNode bmNode)) return true;

                var selectionBitmap = bmNode.Bitmap;
                var emptyBitmap = selectionBitmap.Copy();
                emptyBitmap.Clear();

                var pos = selectionLayer.Position;

                drawingLayer.DrawBitmap(emptyBitmap, pos);

                drawingLayer.RemoveSelectionFromTarget();

                ViewPortService.Refresh();
            }
            else
            {
                drawingLayer.ClearTarget();
            }

            return true;
        }
    }
}