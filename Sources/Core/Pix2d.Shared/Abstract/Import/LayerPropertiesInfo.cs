using SkiaSharp;

namespace Pix2d.Abstract.Import;

public class LayerPropertiesInfo
{
    public float Opacity { get; set; } = 1;

    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    public List<LayerFrameInfo> Frames { get; set; } = [];
}