using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaSharp;

namespace Pix2d.Plugins.ImageFormats.GifFormat.Importers;

public class GifImporter : IImporter
{
    public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget importTarget)
    {
        await using var stream = await files.First().OpenRead();
        using var codec = SKCodec.Create(stream);
        var info = codec.Info;
        info = new SKImageInfo(info.Width, info.Height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);

        var frames = new List<SKBitmap>();

        for (var i = 0; i < codec.FrameCount; i++)
        {
            var opts = new SKCodecOptions(i);
            var frameBitmap = new SKBitmap(info);

            if (codec.GetFrameInfo(i, out _) 
                && codec?.GetPixels(info, frameBitmap.GetPixels(), opts) == SKCodecResult.Success) 
                frames.Add(frameBitmap);
        }

        importTarget.Import(new ImportData(info.Size, frames));
    }
}