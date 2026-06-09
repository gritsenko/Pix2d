using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IEditService
{
    void ShowNodeEditor();

    void HideNodeEditor();

    void RequestEdit(SKNode[] nodes);

    /// <summary>
    /// Makes <paramref name="sprite"/> the active edit target (the sprite drawing/animation acts on).
    /// No-op if it is already active. Used to switch between several artboards on one scene.
    /// </summary>
    void ActivateArtboard(Pix2dSprite sprite);

    /// <summary>
    /// Creates a new empty sprite (artboard) of the given size, places it next to the existing
    /// artboards on the current scene, makes it the active edit target and frames the view. Undoable.
    /// </summary>
    Pix2dSprite AddArtboard(SKSize size);

    void ApplyCurrentEdit();

    void Resize(IContainerNode containerNode, SKSize size);
    void CropCurrentSprite(SKSize size, float horizontalAnchor, float verticalAnchor);
    void CropCurrentSprite(SKRect newBounds);
    void ResizeCurrentSprite(SKSize size);
}