using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using SkiaSharp;

namespace Pix2d.Services;

public class AvaloniaClipboardService : InternalClipboardService
{
    private const string Pix2DClipboardData = "93375907-8CDB-4B00-BFF4-043A99632F42";

    public AvaloniaClipboardService(IDrawingService drawingService, IToolService toolService, IViewPortService viewPortService) : base(drawingService, toolService, viewPortService)
    {
    }

    public override async Task<SKBitmap> GetImageFromClipboard()
    {
        var c = Application.Current.Clipboard;
        // last objects in clipboard from us
        var formats = await c.GetFormatsAsync();
        if (formats.Any(x => x == "PNG"))
        {
            if (await c.GetDataAsync("PNG") is byte[] data)
            {
                var bitmap = SKBitmap.Decode(data);
                return bitmap;
            }
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
