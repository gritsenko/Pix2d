using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class DuplicateAnimationFrameOperation : EditOperationBase
    {
        private readonly Pix2dSprite _sprite;
        private readonly int _previousIndex;
        private readonly int _newFrameIndex;

        public override bool AffectsNodeStructure => false;

        public DuplicateAnimationFrameOperation(Pix2dSprite sprite, int previousIndex)
        {
            _sprite = sprite;

            //previousIndex == -1 means add to end of list
            _previousIndex = previousIndex;
            _newFrameIndex = _previousIndex + 1;
        }

        public override void OnPerform()
        {
            var layers = _sprite.Layers.ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                layers[i].DuplicateFrame(_previousIndex);
            }

            _sprite.SetFrameIndex(_newFrameIndex);
        }

        public override void OnPerformUndo()
        {
            var layers = _sprite.Layers.ToArray();

            _sprite.SetFrameIndex(_previousIndex, true);

            for (var i = 0; i < layers.Length; i++)
            {
                layers[i].DeleteFrame(_newFrameIndex);
            }

        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _sprite.Yield();
        }
    }
}