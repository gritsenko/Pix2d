using System.Threading.Tasks;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IImageCaptureService
{
    Task<SKBitmap> GetImageAsync();
    Task PasteImageAsync();
}