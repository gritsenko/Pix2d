#nullable enable
using Pix2d.Abstract.Platform;
using Pix2d.CommonNodes;
using Pix2d.Plugins.Sprite.Editors;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services;

public class InternalClipboardService : IClipboardService
{
    public IDrawingService DrawingService { get; }
    public IViewPortService ViewPortService { get; }
    public IDialogService DialogService { get; }
    private AppState AppState { get; }

    protected SKBitmap SavedBitmap { get; set; }

    public InternalClipboardService(
        IDrawingService drawingService, 
        IViewPortService viewPortService, 
        IDialogService dialogService,
        AppState appState)
    {
        DrawingService = drawingService;
        ViewPortService = viewPortService;
        DialogService = dialogService;
        AppState = appState;
    }

    public virtual Task<SKBitmap?> GetImageFromClipboard()
    {
        if (SavedBitmap == null)
            return Task.FromResult<SKBitmap>(null);

        return Task.FromResult(SavedBitmap.Copy());
    }

    public virtual async void TryPaste()
    {
        try
        {
            var img = await GetImageFromClipboard();
            if (img != null)
            {
                var dln = DrawingService?.DrawingLayer as SKNode;
                var isResized = await TryToResizeCanvas(dln, img);
                var localPos = dln.GetLocalPosition(ViewPortService.ViewPort.ViewPortCenterGlobal);
                if (isResized)
                {
                    localPos = new SKPoint(0, 0);
                    ViewPortService.ShowAll();
                }

                DrawingService?.PasteBitmap(img, localPos);
            }
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    private async Task<bool> TryToResizeCanvas(SKNode dln, SKBitmap img)
    {
        if (img.Width > dln.Size.Width || img.Height > dln.Size.Height)
        {
            var result = await DialogService.ShowYesNoDialog(
                "Imported image size is bigger then current. Resize current image, so pasted image will fit into it?", "Resize canvas?", "Yes", "No");
            if (result)
            {
                var currentNode =  AppState.CurrentProject.CurrentEditedNode;
                if (currentNode is Pix2dSprite sprite)
                {
                    var maxW = Math.Max(img.Width, sprite.Size.Width);
                    var maxH = Math.Max(img.Height, sprite.Size.Height);
                    var editor = AppState.CurrentProject.CurrentNodeEditor as SpriteEditor;
                    editor?.Resize((int)maxW, (int)maxH);
                    return true;
                }
            }
        }

        return false;
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