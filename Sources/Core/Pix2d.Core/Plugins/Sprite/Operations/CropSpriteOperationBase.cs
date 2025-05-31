using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Operations;

public class CropSpriteOperationBase(Pix2dSprite targetSprite, SKRect newBounds)
    : EditSpriteOperationBase(targetSprite), IUpdateDrawingTarget
{
    private readonly SKRect _oldBounds = targetSprite.GetBoundingBox();

    public override void OnPerform()
    {
        _targetSprite.Crop(newBounds);

        if (!HasFinalStates)
            SetFinalData();

        base.OnPerform();
    }

    public override void OnPerformUndo()
    {
        base.OnPerformUndo();
        _targetSprite.Crop(_oldBounds);

        SetFramesData(_targetSprite, _unmodifidSpriteData);
    }
}