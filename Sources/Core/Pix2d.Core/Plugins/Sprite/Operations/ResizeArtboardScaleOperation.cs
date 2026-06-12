using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Operations;

/// <summary>
/// Single undoable operation for the "edit sprite as object" resize: scales the artboard's pixel content to
/// <paramref name="newSize"/> (nearest-neighbour, via <see cref="Pix2dSprite.ResizeImage"/>) AND moves the
/// sprite to <paramref name="newPosition"/> so the anchored corner stays put in world space. The crop-mode
/// twin is <see cref="ResizeArtboardOperation"/> (which changes the canvas without scaling).
///
/// IMPORTANT: the caller MUST reset the sprite to its original Position/Size *before* constructing this
/// operation — the base (<see cref="EditSpriteOperationBase"/>) snapshots the initial transform states and
/// pixels in its constructor, so the sprite must be in its pre-edit state at that point.
/// </summary>
public class ResizeArtboardScaleOperation(Pix2dSprite targetSprite, SKSize newSize, SKPoint newPosition)
    : EditSpriteOperationBase(targetSprite), IUpdateDrawingTarget
{
    private readonly SKSize _oldSize = targetSprite.Size;

    public override void OnPerform()
    {
        _targetSprite.ResizeImage(newSize); // scales pixel content to the new canvas size
        _targetSprite.Position = newPosition;

        if (!HasFinalStates)
            SetFinalData();

        base.OnPerform();
    }

    public override void OnPerformUndo()
    {
        // base restores Position/Size of the sprite and all descendants from the initial transform states;
        // resize the canvas back to the old size and restore the original pixels (mirrors ResizeSpriteOperationBase).
        base.OnPerformUndo();
        _targetSprite.Resize(_oldSize);
        SetFramesData(_targetSprite, _unmodifidSpriteData);
    }
}
