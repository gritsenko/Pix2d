using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Common.Extensions;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;
using SkiaNodes.Extensions;

namespace Pix2d.Services;

public abstract class BaseAvaloniaClipboardService(
    IDrawingService drawingService,
    IViewPortService viewPortService,
    IDialogService dialogService,
    AppState appState)
    : InternalClipboardService(drawingService, viewPortService, dialogService, appState)
{
    protected abstract IClipboard? Clipboard { get; }

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

    protected virtual async Task PutImageIntoClipboard(SKBitmap? bitmap)
    {
        if (bitmap == null || Clipboard == null)
            return;

        var bytes = bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
#pragma warning disable CS0618
        var dataObject = new DataObject();
        dataObject.Set("PNG", bytes);

        await Clipboard.ClearAsync();
        await Clipboard.SetDataObjectAsync(dataObject);
#pragma warning restore CS0618
    }

    public override async Task<SKBitmap?> GetImageFromClipboard()
    {
        var supportedFormats = new HashSet<string>(new[]
        {
            "PNG", "image/png", "image/webp", "image/jpeg", "image/bmp", "image/ico", "image/icon", "image/tiff"
        });

        if (Clipboard == null)
            return await base.GetImageFromClipboard();

#pragma warning disable CS0618
        // 1. Try TryGetBitmapAsync
        try
        {
            var bitmap = await Clipboard.TryGetBitmapAsync();
            if (bitmap is Avalonia.Media.Imaging.Bitmap standardBitmap)
            {
                return standardBitmap.ToSKBitmap();
            }
        }
        catch { /* ignored */ }

        // 2. Try TryGetFileAsync
        try
        {
            var storageItem = await Clipboard.TryGetFileAsync();
            if (storageItem is Avalonia.Platform.Storage.IStorageFile file)
            {
                using var stream = await file.OpenReadAsync();
                return stream.ToSKBitmap();
            }
        }
        catch { /* ignored */ }

        // 3. Fallback to format-based GetDataAsync
        var formats = await Clipboard.GetFormatsAsync();
        if (formats != null)
        {
            foreach (var format in formats)
            {
                if (supportedFormats.Contains(format))
                {
                    var data = await Clipboard.GetDataAsync(format);
                    if (data is byte[] byteData)
                    {
                        return SKBitmap.Decode(byteData);
                    }
                }
            }
        }
#pragma warning restore CS0618

        return await base.GetImageFromClipboard();
    }
}
