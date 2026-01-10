using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Operations;

public class ResizeSpriteOperationBase(Pix2dSprite targetSprite, SKSize newSize) : EditSpriteOperationBase(targetSprite)
{
    private SKSize _oldSize = targetSprite.Size;

    public override void OnPerform()
    {
        _targetSprite.ResizeImage(newSize);

        if (!HasFinalStates)
            SetFinalData();

        base.OnPerform();
    }

    public override void OnPerformUndo()
    {
        base.OnPerformUndo();
        _targetSprite.Resize(_oldSize);

        SetFramesData(_targetSprite, _unmodifidSpriteData);
    }
}