using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class ResizeSpriteOperationBase : EditSpriteOperationBase
    {
        private SKSize _newSize;
        private SKSize _oldSize;

        public ResizeSpriteOperationBase(Pix2dSprite targetSprite, SKSize newSize) : base(targetSprite)
        {
            _newSize = newSize;
            _oldSize = targetSprite.Size;
        }

        public override void OnPerform()
        {
            _targetSprite.Resize(_newSize);
            
            if(!HasFinalStates)
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
}