using SkiaSharp;

namespace Pix2d.Abstract.Import;

public class LayerPropertiesInfo
{
    /// <summary>
    /// Layer name carried over from the source document, or null to keep the auto-generated one. Only
    /// layered formats (.piskel, and .psd once it lands) have anything to put here — a PNG/GIF import
    /// leaves it null so the sprite keeps its "Layer 000" naming.
    /// </summary>
    public string? Name { get; set; }

    public float Opacity { get; set; } = 1;

    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    public List<LayerFrameInfo> Frames { get; set; } = [];
}