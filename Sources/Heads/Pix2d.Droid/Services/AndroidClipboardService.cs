using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Android.Content;
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
    : InternalClipboardService(drawingService, viewPortService, dialogService, appState)
{
    private ClipboardManager? Clipboard => Android.App.Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;

    public override async Task<bool> TryCopyNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
    {
        var result = await base.TryCopyNodesAsBitmapAsync(nodes, backgroundColor);
        if (result && SavedBitmap != null)
        {
            await PutImageIntoClipboard(SavedBitmap);
        }
        return result;
    }

    public override async Task<bool> TryCutNodesAsBitmapAsync(IEnumerable<SKNode> nodes, SKColor backgroundColor)
    {
        var result = await base.TryCutNodesAsBitmapAsync(nodes, backgroundColor);
        if (result && SavedBitmap != null)
        {
            await PutImageIntoClipboard(SavedBitmap);
        }
        return result;
    }

    private Task PutImageIntoClipboard(SKBitmap bitmap)
    {
        if (Clipboard == null)
            return Task.CompletedTask;

        try
        {
            // Android clipboard doesn't directly support images in the same way as Windows/macOS.
            // Usually we'd use a ContentProvider to share the image URI.
            // For now, let's focus on getting images FROM the clipboard, 
            // as copying TO external apps from Android might require more setup (ContentProvider).
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        return Task.CompletedTask;
    }

    public override async Task<SKBitmap?> GetImageFromClipboard()
    {
        if (Clipboard == null || !Clipboard.HasPrimaryClip)
            return null;

        var clipData = Clipboard.PrimaryClip;
        if (clipData == null)
            return null;

        // --- Шаг 1: Проверяем extras в ClipDescription ---
        // Chrome и ряд других приложений кладут content:// URI изображения сюда
        var description = clipData.Description;
        if (description != null)
        {
            var extras = description.Extras;
            if (extras != null)
            {
                // PersistableBundle поддерживает только примитивы и строки,
                // поэтому URI может быть только строкой
                Android.Net.Uri? extraUri = null;

                // Перебираем все ключи — не знаем точный ключ заранее
                var keys = extras.KeySet();
                if (keys != null)
                {
                    foreach (var key in keys)
                    {
                        System.Diagnostics.Debug.WriteLine($"  extras key='{key}' value='{extras.GetString(key)}'");
                        var val = extras.GetString(key);
                        if (!string.IsNullOrEmpty(val))
                        {
                            var parsed = Android.Net.Uri.Parse(val);
                            if (parsed?.Scheme != null)
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

            // --- Шаг 2: Смотрим MIME-типы в описании ---
            // Если есть image/* MIME — URI должен быть где-то в item
            bool hasImageMime = false;
            for (int m = 0; m < description.MimeTypeCount; m++)
            {
                var mime = description.GetMimeType(m);
                if (mime != null && mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    hasImageMime = true;
                    break;
                }
            }

            // Логируем для диагностики
            System.Diagnostics.Debug.WriteLine(
                $"ClipDescription MIME count: {description.MimeTypeCount}");
            for (int m = 0; m < description.MimeTypeCount; m++)
                System.Diagnostics.Debug.WriteLine($"  MIME[{m}]: {description.GetMimeType(m)}");
        }

        // --- Шаг 3: Стандартный перебор items ---
        for (int i = 0; i < clipData.ItemCount; i++)
        {
            var item = clipData.GetItemAt(i);
            if (item == null) continue;

            System.Diagnostics.Debug.WriteLine(
                $"Item[{i}]: Uri={item.Uri}, Text={item.Text}, Intent={item.Intent}");

            // Priority 1: Direct URI
            if (item.Uri != null)
            {
                var bitmap = await TryGetBitmapFromUriAsync(item.Uri);
                if (bitmap != null) return bitmap;
            }

            // Priority 2: coerceToText может вернуть URI строкой
            // (когда item.Text == null, но внутри есть URI)
            if (item.Text == null && item.Uri == null)
            {
                try
                {
                    var context = Android.App.Application.Context;
                    var coerced = item.CoerceToText(context)?.ToString();
                    System.Diagnostics.Debug.WriteLine($"  CoercedText: {coerced}");
                    if (!string.IsNullOrWhiteSpace(coerced))
                    {
                        var parsed = Android.Net.Uri.Parse(coerced);
                        if (parsed?.Scheme != null)
                        {
                            var bmp = await TryGetBitmapFromUriAsync(parsed);
                            if (bmp != null) return bmp;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CoerceToText failed: {ex.Message}");
                }
            }

            // Priority 3: Text как URL
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
                        var bitmap = await TryGetBitmapFromUriAsync(uri);
                        if (bitmap != null) return bitmap;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to parse text as URI: {ex.Message}");
                    }
                }
            }
        }

        return null;
    }

    private async Task<SKBitmap?> TryGetBitmapFromUriAsync(Android.Net.Uri uri)
    {
        try
        {
            var scheme = uri.Scheme?.ToLowerInvariant();

            // Case A: Internet URL
            if (scheme == "http" || scheme == "https")
            {
                using var client = new HttpClient();
                // Download the image data
                var bytes = await client.GetByteArrayAsync(uri.ToString());
                return SKBitmap.Decode(bytes);
            }

            // Case B: Local Content Provider (content://...)
            var contentResolver = Android.App.Application.Context.ContentResolver;
            if (contentResolver == null) return null;

            // Verify MIME type if possible
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
