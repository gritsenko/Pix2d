using SkiaSharp;

namespace SkiaNodes.Abstract
{
    public interface IContainerNode
    {
        SKColor BackgroundColor { get; set; }
        bool UseBackgroundColor { get; set; }

        SKSize Size { get; set; }
        void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor);
    }
}
