using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using SkiaNodes;
using SkiaSharp;
using SkiaNodes.Extensions;

namespace Pix2d.Services;

public class AvaloniaClipboardService : InternalClipboardService
{
    private const string Pix2DClipboardData = "93375907-8CDB-4B00-BFF4-043A99632F42";

    IClipboard? Clipboard => EditorApp.TopLevel?.Clipboard;

    public AvaloniaClipboardService(IDrawingService drawingService, IToolService toolService, IViewPortService viewPortService) : base(drawingService, toolService, viewPortService)
    {
    }

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

    private async Task PutImageIntoClipboard(SKBitmap bitmap)
    {

        var bytes = bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        var dataObject = new DataObject();
        dataObject.Set("PNG", bytes);

        await Clipboard.ClearAsync();
        await Clipboard.SetDataObjectAsync(dataObject);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }
        var bm = System.Drawing.Bitmap.FromStream(bitmap.ToPngStream()) as System.Drawing.Bitmap;
        Clowd.Clipboard.ClipboardGdi.SetImage(bm);
    }

    public override async Task<SKBitmap?> GetImageFromClipboard()
    {
        // last objects in clipboard from us
        var formats = await Clipboard.GetFormatsAsync();
        if (formats.Any(x => x == "PNG"))
        {
            if (await Clipboard.GetDataAsync("PNG") is byte[] data)
            {
                var bitmap = SKBitmap.Decode(data);
                return bitmap;
            }
        }

        //windows specific clipboard format
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var image = await Clowd.Clipboard.ClipboardGdi.GetImageAsync();

            if (image == null)
                return null;

            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            var skBitmap = SKBitmap.Decode(ms.GetBuffer());
            return skBitmap;
        }

        return null;
    }

    private SKBitmap ImageFromClipboardDib(MemoryStream ms)
    {
        //MemoryStream ms = Application.Current.Clipboard.GetData("DeviceIndependentBitmap") as MemoryStream;
        if (ms != null)
        {
            byte[] dibBuffer = new byte[ms.Length];
            ms.Read(dibBuffer, 0, dibBuffer.Length);

            BITMAPINFOHEADER infoHeader =
                BinaryStructConverter.FromByteArray<BITMAPINFOHEADER>(dibBuffer);

            int fileHeaderSize = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
            int infoHeaderSize = infoHeader.biSize;
            int fileSize = fileHeaderSize + infoHeader.biSize + infoHeader.biSizeImage;

            BITMAPFILEHEADER fileHeader = new BITMAPFILEHEADER();
            fileHeader.bfType = BITMAPFILEHEADER.BM;
            fileHeader.bfSize = fileSize;
            fileHeader.bfReserved1 = 0;
            fileHeader.bfReserved2 = 0;
            fileHeader.bfOffBits = fileHeaderSize + infoHeaderSize + infoHeader.biClrUsed * 4;

            byte[] fileHeaderBytes =
                BinaryStructConverter.ToByteArray(fileHeader);

            MemoryStream msBitmap = new MemoryStream();
            msBitmap.Write(fileHeaderBytes, 0, fileHeaderSize);
            msBitmap.Write(dibBuffer, 0, dibBuffer.Length);
            msBitmap.Seek(0, SeekOrigin.Begin);

            return SKBitmap.Decode(msBitmap);
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct BITMAPFILEHEADER
    {
        public static readonly short BM = 0x4d42; // BM

        public short bfType;
        public int bfSize;
        public short bfReserved1;
        public short bfReserved2;
        public int bfOffBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

}


public static class BinaryStructConverter
{
    public static T FromByteArray<T>(byte[] bytes) where T : struct
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            int size = Marshal.SizeOf(typeof(T));
            ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, ptr, size);
            object obj = Marshal.PtrToStructure(ptr, typeof(T));
            return (T)obj;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
    }

    public static byte[] ToByteArray<T>(T obj) where T : struct
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            int size = Marshal.SizeOf(typeof(T));
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, true);
            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
    }
}
