using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Pix2d.Abstract.Services;
using Pix2d.State;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services;

public class DesktopClipboardService(
    IDrawingService drawingService,
    IViewPortService viewPortService,
    IDialogService dialogService,
    AppState appState)
    : BaseAvaloniaClipboardService(drawingService, viewPortService, dialogService, appState)
{
    protected override IClipboard? Clipboard => EditorApp.TopLevel?.Clipboard;

    protected override async Task PutImageIntoClipboard(SKBitmap? bitmap)
    {
        await base.PutImageIntoClipboard(bitmap);

        if (bitmap == null || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        PutWindowsImageIntoClipboard(bitmap);
    }
    
    public override async Task<SKBitmap?> GetImageFromClipboard()
    {
        var result = await base.GetImageFromClipboard();
        if (result != null)
            return result;

        //windows specific clipboard format
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GetWindowsImageFromClipboardAsync();
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static void PutWindowsImageIntoClipboard(SKBitmap bitmap)
    {
        using var pngStream = bitmap.ToPngStream();
        using var image = System.Drawing.Bitmap.FromStream(pngStream) as System.Drawing.Bitmap;
        if (image != null)
            Clowd.Clipboard.ClipboardGdi.SetImage(image);
    }

    [SupportedOSPlatform("windows")]
    private static async Task<SKBitmap?> GetWindowsImageFromClipboardAsync()
    {
        using var image = await Clowd.Clipboard.ClipboardGdi.GetImageAsync();

        if (image == null)
            return null;

        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        return SKBitmap.Decode(ms);
    }
}
