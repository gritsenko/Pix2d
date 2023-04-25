namespace Pix2d.Abstract.UI;

public interface IPanelsController
{
    bool ShowLayers { get; set; }
    bool ShowLayerProperties { get; set; }
    bool ShowCanvasResizePanel { get; set; }
}