using SkiaSharp;

namespace Pix2d.Abstract.Import;

public class ImportData
{
    /// <summary>
    /// Import info for simple animation or single image with only one layer, like png, jpg or gif
    /// </summary>
    /// <param name="size">General size of the image</param>
    /// <param name="frames">list of frames represented by SKBitmaps</param>
    /// <param name="replaceFrames">Delete current frame before import</param>
    public ImportData(SKSizeI size, List<SKBitmap> frames, bool replaceFrames = true)
    {
        Size = size;
        var layerInfo = new LayerPropertiesInfo();
        Layers.Add(layerInfo);
        layerInfo.Frames = frames
            .Select(x => new LayerFrameInfo { BitmapProviderFunc = () => x })
            .ToList();
        ReplaceFrames = replaceFrames;
    }

    public bool ReplaceFrames { get; set; }
    public SKSizeI Size { get; set; }
    public List<LayerPropertiesInfo> Layers { get; set; } = [];
}