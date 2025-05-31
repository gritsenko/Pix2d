using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IEditService
{
    void ShowNodeEditor();

    void HideNodeEditor();

    void RequestEdit(SKNode[] nodes);
    void ApplyCurrentEdit();

    void Resize(IContainerNode containerNode, SKSize size);
    void CropCurrentSprite(SKSize size, float horizontalAnchor, float verticalAnchor);
    void CropCurrentSprite(SKRect newBounds);
}