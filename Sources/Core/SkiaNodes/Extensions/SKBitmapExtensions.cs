using SkiaSharp;

namespace SkiaNodes.Extensions;

public static class SKBitmapExtensions
{
    public static SKBitmap ToSKBitmap(this Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        return DecodeBitmap(codec);
    }

    private static SKBitmap DecodeBitmap(SKCodec codec)
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

    public static SKBitmap ToSKBitmap(this byte[] data)
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
        return SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height, bitmap.ColorType, SKAlphaType.Premul), bitmap.GetPixels(), bitmap.Width * 4);
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

    public static SKBitmap Resize(this SKBitmap bitmap, SKSizeI newSize, float horizontalAnchor, float verticalAnchor)
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
        var newBm = new SKBitmap(new SKImageInfo(newSize.Width, newSize.Height, SKColorType.Rgba8888));
        newBm.Erase(SKColor.Empty);
        using (var canvas = newBm.GetSKSurface().Canvas)
        {
            processAction(canvas);
        }
        return newBm;
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