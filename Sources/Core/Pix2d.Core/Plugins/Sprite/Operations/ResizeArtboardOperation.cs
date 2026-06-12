using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Operations;

/// <summary>
/// Single undoable operation for the "edit sprite as object" resize: crops the artboard canvas to
/// <paramref name="newLocalBounds"/> (crop-tool semantics — no pixel scaling; growing adds transparent
/// pixels, shrinking trims them) AND moves the sprite to <paramref name="newPosition"/> so the kept
/// content stays anchored in world space. Mirrors <see cref="CropSpriteOperationBase"/>, but additionally
/// applies the new <see cref="SKNode.Position"/>.
///
/// IMPORTANT: the caller MUST reset the sprite to its original Position/Size *before* constructing this
/// operation — the base (<see cref="EditSpriteOperationBase"/>) snapshots the initial transform states and
/// pixels in its constructor, so the sprite must be in its pre-edit state at that point.
/// </summary>
public class ResizeArtboardOperation(Pix2dSprite targetSprite, SKRect newLocalBounds, SKPoint newPosition)
    : EditSpriteOperationBase(targetSprite), IUpdateDrawingTarget
{
    // Local bounds that restore the original canvas size on undo (sprite content origin is local 0,0).
    private readonly SKRect _oldLocalBounds = new(0, 0, targetSprite.Size.Width, targetSprite.Size.Height);

    public override void OnPerform()
    {
        _targetSprite.Crop(newLocalBounds);
        _targetSprite.Position = newPosition;

        if (!HasFinalStates)
            SetFinalData();

        base.OnPerform();
    }

    public override void OnPerformUndo()
    {
        // base restores Position/Size of the sprite and all descendants from the initial transform states;
        // re-crop to the old canvas size and restore the original pixels (same pattern as CropSpriteOperationBase).
        base.OnPerformUndo();
        _targetSprite.Crop(_oldLocalBounds);
        SetFramesData(_targetSprite, _unmodifidSpriteData);
    }
}
