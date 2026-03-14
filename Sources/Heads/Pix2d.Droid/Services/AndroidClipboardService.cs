using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Android.Content;
using Avalonia.Input.Platform;
using Pix2d.Abstract.Services;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services;

public class AndroidClipboardService(
    IDrawingService drawingService,
    IViewPortService viewPortService,
    IDialogService dialogService,
    AppState appState)
    : BaseAvaloniaClipboardService(drawingService, viewPortService, dialogService, appState)
{
    protected override IClipboard? Clipboard => EditorApp.TopLevel?.Clipboard;

    private ClipboardManager? AndroidClipboard => Android.App.Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;

    public override async Task<SKBitmap?> GetImageFromClipboard()
    {
        // 1. Try base Avalonia-based logic first (internal + Avalonia API)
        var result = await base.GetImageFromClipboard();
        if (result != null)
            return result;

        // 2. Fallback to Android-specific logic (URI, ContentProvider, etc.)
        if (AndroidClipboard == null || !AndroidClipboard.HasPrimaryClip)
            return null;

        var clipData = AndroidClipboard.PrimaryClip;
        if (clipData == null)
            return null;

        // --- Step 1: Check extras in ClipDescription ---
        var description = clipData.Description;
        if (description != null)
        {
            var extras = description.Extras;
            if (extras != null)
            {
                Android.Net.Uri? extraUri = null;
                var keys = extras.KeySet();
                if (keys != null)
                {
                    foreach (var key in keys)
                    {
                        var val = extras.GetString(key);
                        if (!string.IsNullOrEmpty(val))
                        {
                            var parsed = Android.Net.Uri.Parse(val);
                            if (parsed != null && parsed.Scheme != null)
                            {
                                extraUri = parsed;
                                break;
                            }
                        }
                    }
                }

                if (extraUri != null)
                {
                    var bmp = await TryGetBitmapFromUriAsync(extraUri);
                    if (bmp != null) return bmp;
                }
            }
        }
        
        for (int i = 0; i < clipData.ItemCount; i++)
        {
            var item = clipData.GetItemAt(i);
            if (item == null) continue;

            // Priority: Direct URI
            if (item.Uri != null)
            {
                var bitmap = await TryGetBitmapFromUriAsync(item.Uri);
                if (bitmap != null) return bitmap;
            }

            // Fallback: Coerced text (might be a URI string)
            if (item.Text == null && item.Uri == null)
            {
                try
                {
                    var context = Android.App.Application.Context;
                    var coerced = item.CoerceToText(context)?.ToString();
                    if (!string.IsNullOrWhiteSpace(coerced))
                    {
                        var parsed = Android.Net.Uri.Parse(coerced);
                        if (parsed != null && parsed.Scheme != null)
                        {
                            var bmp = await TryGetBitmapFromUriAsync(parsed);
                            if (bmp != null) return bmp;
                        }
                    }
                }
                catch { /* ignored */ }
            }

            // Fallback: Text as URL/Content URI
            if (item.Text != null)
            {
                var text = item.Text.ToString();
                if (!string.IsNullOrWhiteSpace(text) &&
                    (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                     text.StartsWith("content://", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var uri = Android.Net.Uri.Parse(text);
                        if (uri != null)
                        {
                            var bitmap = await TryGetBitmapFromUriAsync(uri);
                            if (bitmap != null) return bitmap;
                        }
                    }
                    catch { /* ignored */ }
                }
            }
        }

        return null;
    }

    private async Task<SKBitmap?> TryGetBitmapFromUriAsync(Android.Net.Uri uri)
    {
        if (uri == null) return null;
        try
        {
            var scheme = uri.Scheme?.ToLowerInvariant();

            // Case A: Internet URL
            if (scheme == "http" || scheme == "https")
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(uri.ToString());
                return SKBitmap.Decode(bytes);
            }

            // Case B: Local Content Provider (content://...)
            var contentResolver = Android.App.Application.Context.ContentResolver;
            if (contentResolver == null) return null;

            var type = contentResolver.GetType(uri);
            if (type != null && !type.StartsWith("image/"))
            {
                return null;
            }

            using var stream = contentResolver.OpenInputStream(uri);
            if (stream == null) return null;

            using var memStream = new MemoryStream();
            await stream.CopyToAsync(memStream);
            memStream.Position = 0;

            return SKBitmap.Decode(memStream);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return null;
        }
    }
}
