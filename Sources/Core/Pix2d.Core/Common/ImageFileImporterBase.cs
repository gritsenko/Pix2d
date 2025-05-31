using Pix2d.Abstract.Platform.FileSystem;
using SkiaSharp;
using SkiaNodes.Extensions;
using Pix2d.Abstract.Import;

namespace Pix2d.Common;

public class ImageFileImporterBase : IImporter
{

    public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget importTarget)
    {
        var frames = new List<SKBitmap>();
        int w = 0, h = 0;
        foreach (var file in files)
        {
            await using var stream = await file.OpenRead();
            var bm = stream.ToSKBitmap();
            frames.Add(bm);
            w = Math.Max(w, bm.Width);
            h = Math.Max(h, bm.Height);
        }

        importTarget.Import(new ImportData(new SKSizeI(w, h), frames));
    }
}