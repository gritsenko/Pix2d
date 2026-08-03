using SkiaSharp;

namespace SkiaNodes.Extensions;

public static class SKBitmapExtensions
{
    public static SKBitmap? ToSKBitmap(this Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        return DecodeBitmap(codec);
    }

    private static SKBitmap? DecodeBitmap(SKCodec? codec)
    {
        if (codec == null)
            return null;

        var info = codec.Info;
        info.ColorType = SKApp.ColorType;
        info.AlphaType = SKAlphaType.Premul;
        var srcBm = SKBitmap.Decode(codec, info);

        //hack to load premultiplied alpha image without artifacts
        //var bm = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        //bm.Erase(SKColor.Empty);
        //using (var surface = SKSurface.Create(bm.Info, bm.GetPixels(), bm.Width * 4))
        //using (surface.Canvas)
        //{
        //    surface.Canvas.DrawBitmap(srcBm, 0, 0);
        //    surface.Canvas.Flush();
        //}

        return srcBm;
    }

    public static SKBitmap? ToSKBitmap(this byte[] data)
    {
        using (var skMemoryStream = new SKMemoryStream(data))
        using (var codec = SKCodec.Create(skMemoryStream))
        {
            return DecodeBitmap(codec);
        }
    }

    public static Stream ToPngStream(this SKBitmap bitmap)
    {
        var img = SKImage.FromBitmap(bitmap);
        var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.AsStream(true);
    }

    public static Stream ToJpgStream(this SKBitmap bitmap, int quality = 60)
    {
        var img = SKImage.FromBitmap(bitmap);
        var data = img.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.AsStream(true);
    }

    public static SKSurface GetSKSurface(this SKBitmap bitmap)
    {
        return SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height, bitmap.ColorType, SKAlphaType.Premul), bitmap.GetPixels(), bitmap.RowBytes);
    }

    /// <summary>
    /// Opens a canvas that draws straight into <paramref name="bitmap"/>'s pixels. Use this instead of
    /// <c>bitmap.GetSKSurface().Canvas</c>.
    ///
    /// <para>That idiom is a trap in two ways: the surface it creates is never disposed (the <c>using</c>
    /// binds the canvas, which the surface owns), and it can hand back a <b>null</b> canvas — a raster
    /// surface over an unallocated pixel buffer, or a native handle SkiaSharp's object cache no longer
    /// resolves after the leaked surface was finalized. The caller then dereferenced null deep inside a
    /// draw lambda: appstat saw it as a bare `NullReferenceException` in
    /// <c>SKBitmapExtensions.CropByAnchor</c> under a resize undo, 23 events / 6 users on 3.11.2, with
    /// nothing in the stack pointing at the missing canvas. A canvas built directly on the bitmap owns
    /// itself, needs no surface, and throws a named error if it cannot be created.</para>
    /// </summary>
    public static SKCanvas CreateCanvas(this SKBitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        // SKBitmap's ctor throws when it cannot allocate, but a bitmap can also *arrive* here with its
        // pixels released (see BitmapNode's unload path), and Skia will not raster into nothing.
        if (bitmap.GetPixels() == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Cannot draw into a {bitmap.Width}x{bitmap.Height} bitmap: it has no pixel buffer.");

        return new SKCanvas(bitmap);
    }

    public static void Clear(this SKBitmap bitmap)
    {
        bitmap.Erase(SKColor.Empty);
    }


    public static SKBitmap FlipHorizontal(this SKBitmap bitmap)
    {
        return ProcessBitmap(bitmap, canvas =>
        {
            canvas.Scale(-1, 1, bitmap.Width / 2f, bitmap.Height / 2f);
            canvas.DrawBitmap(bitmap, 0, 0);
        });
    }

    public static SKBitmap FlipVertical(this SKBitmap bitmap)
    {
        return ProcessBitmap(bitmap, canvas =>
        {
            canvas.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
            canvas.DrawBitmap(bitmap, 0, 0);
        });
    }

    public static SKBitmap Rotate90(this SKBitmap bitmap)
    {
        var w = bitmap.Height;
        var h = bitmap.Width;

        return ProcessBitmap(new SKSizeI(w, h), canvas =>
        {
            canvas.RotateDegrees(90, w / 2f, h / 2f);
            canvas.DrawBitmap(bitmap, (w - bitmap.Width) / 2f, (h - bitmap.Height) / 2f);
        });
    }

    public static SKBitmap CropByAnchor(this SKBitmap bitmap, SKSizeI newSize, float horizontalAnchor, float verticalAnchor)
    {
        return ProcessBitmap(newSize, canvas =>
        {
            canvas.DrawBitmap(bitmap, horizontalAnchor * (newSize.Width - bitmap.Width), verticalAnchor * (newSize.Height - bitmap.Height));
        });
    }

    public static SKBitmap Crop(this SKBitmap bitmap, SKRect targetBounds)
    {
        var newSize = targetBounds.Size.ToSizeI();

        return ProcessBitmap(newSize, canvas =>
        {
            canvas.DrawBitmap(bitmap, -targetBounds.Left, -targetBounds.Top);
        });

    }

    private static SKBitmap ProcessBitmap(SKBitmap bitmap, Action<SKCanvas> processAction)
        => ProcessBitmap(new SKSizeI(bitmap.Width, bitmap.Height), processAction);

    private static SKBitmap ProcessBitmap(SKSizeI newSize, Action<SKCanvas> processAction)
    {
        // Last line of defence against a 0x0 bitmap entering the model: every crop/resize/rotate lands
        // here, and a sub-pixel crop rect truncates to 0 in ToSizeI(). A zero-sized bitmap allocates
        // fine and then fails much later and far away — GetPixels() is null, so the drawing pipeline
        // throws on the next stroke instead of at the point that produced it.
        newSize = new SKSizeI(Math.Max(1, newSize.Width), Math.Max(1, newSize.Height));

        var newBm = AllocateBitmap(new SKImageInfo(newSize.Width, newSize.Height, SKColorType.Rgba8888));
        newBm.Erase(SKColor.Empty);
        using (var canvas = newBm.CreateCanvas())
        {
            processAction(canvas);
        }
        return newBm;
    }

    /// <summary>
    /// Allocates a bitmap, reporting *what* could not be allocated when it fails. SkiaSharp's own message
    /// ("Unable to allocate pixels for the bitmap.") carries no dimensions, so a report of it says nothing
    /// about whether the app asked for something impossible or the device was simply out of memory —
    /// which is exactly what the 64344556x64 canvas in appstat took an `app_context` field to work out.
    /// </summary>
    private static SKBitmap AllocateBitmap(SKImageInfo info)
    {
        try
        {
            return new SKBitmap(info);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Failed to allocate a {info.Width}x{info.Height} bitmap ({info.BytesSize64 / (1024 * 1024)} MB).", e);
        }
    }

    public unsafe static void CopyPixelsToBitmap(this SKBitmap targetBitmap, byte[] pixels)
    {
        fixed (byte* pSource = pixels)
        {
            Buffer.MemoryCopy(pSource, targetBitmap.GetPixels().ToPointer(), pixels.Length, pixels.Length);
        }
    }
    public unsafe static void CopyFrom(this SKBitmap targetBitmap, SKBitmap sourceBitmap)
    {
        var count = sourceBitmap.ByteCount;
        unsafe
        {
            Buffer.MemoryCopy(sourceBitmap.GetPixels().ToPointer(), targetBitmap.GetPixels().ToPointer(), count, count);
        }
    }
}