using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class CropSpriteOperationBase : EditSpriteOperationBase
    {
        private SKRect _newBounds;
        private SKRect _oldBounds;

        public CropSpriteOperationBase(Pix2dSprite targetSprite, SKRect newBounds) : base(targetSprite)
        {
            _newBounds = newBounds;
            _oldBounds = targetSprite.GetBoundingBox();
        }

        public override void OnPerform()
        {
            _targetSprite.Crop(_newBounds);
            
            if(!HasFinalStates)
                SetFinalData();
            
            CoreServices.DrawingService.UpdateDrawingTarget();
            base.OnPerform();
        }

        public override void OnPerformUndo()
        {
            base.OnPerformUndo();
            _targetSprite.Crop(_oldBounds);

            CoreServices.DrawingService.UpdateDrawingTarget();

            SetFramesData(_targetSprite, _unmodifidSpriteData);
        }
    }
}