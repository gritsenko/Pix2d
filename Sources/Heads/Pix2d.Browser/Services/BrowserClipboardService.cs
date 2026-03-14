using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Services;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Browser.Services;

public class BrowserClipboardService(
    IDrawingService drawingService,
    IViewPortService viewPortService,
    IDialogService dialogService,
    AppState appState)
    : InternalClipboardService(drawingService, viewPortService, dialogService, appState)
{
    private const string Pix2DClipboardData = "93375907-8CDB-4B00-BFF4-043A99632F42";

    private IClipboard? Clipboard => EditorApp.TopLevel?.Clipboard;

    public override async Task<bool> TryCopyNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
    {
        var result = await base.TryCopyNodesAsBitmapAsync(nodes, backgroundColor);
        if (result)
            await PutImageIntoClipboard(SavedBitmap);
        return result;
    }

    public override async Task<bool> TryCutNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
    {
        var result = await base.TryCutNodesAsBitmapAsync(nodes, backgroundColor);
        if (result)
            await PutImageIntoClipboard(SavedBitmap);
        return result;
    }

    private async Task PutImageIntoClipboard(SKBitmap? bitmap)
    {
        if (bitmap == null || Clipboard == null)
            return;

        var bytes = bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        
        var dataObject = new DataObject();
        dataObject.Set("PNG", bytes);

        await Clipboard.ClearAsync();
        await Clipboard.SetDataObjectAsync(dataObject);
    }

    public override async Task<SKBitmap?> GetImageFromClipboard()
    {
        var supportedFormats = new[]
        {
            "PNG", "image/png", "image/webp", "image/jpeg", "image/bmp", "image/ico", "image/icon", "image/tiff"
        };

        if (Clipboard == null)
            return null;

        var formats = await Clipboard.GetFormatsAsync();
        if (formats != null)
        {
            foreach (var format in supportedFormats.Where(x => formats.Contains(x)))
            {
                var data = await Clipboard.GetDataAsync(format);
                if (data is byte[] byteData)
                {
                    var bitmap = SKBitmap.Decode(byteData);
                    return bitmap;
                }
            }
        }

        return await base.GetImageFromClipboard();
    }
}
