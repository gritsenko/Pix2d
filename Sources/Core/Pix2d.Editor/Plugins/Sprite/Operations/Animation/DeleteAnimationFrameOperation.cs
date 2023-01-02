using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class DeleteAnimationFrameOperation : EditOperationBase
    {
        private readonly Pix2dSprite _sprite;
        private readonly Dictionary<int, BitmapNode> _deletedNodes = new Dictionary<int, BitmapNode>();
        private readonly int _deletedFrameIndex;
        private readonly int _newFrameIndex;

        public override bool AffectsNodeStructure => true;

        public DeleteAnimationFrameOperation(Pix2dSprite sprite, int frameIndex)
        {
            _sprite = sprite;

            //addAfterFrameIndex == -1 means add to end of list
            _deletedFrameIndex = frameIndex;
            _newFrameIndex = Math.Max(0,_deletedFrameIndex - 1);
        }

        public override void OnPerform()
        {
            var layers = _sprite.Layers.ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];

                var i1 = i;//resharper idea 
                layer.DeleteFrame(_deletedFrameIndex, s => _deletedNodes[i1] = s);
            }

            _sprite.SetFrameIndex(_newFrameIndex);
        }

        public override void OnPerformUndo()
        {
            var layers = _sprite.Layers.ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];

                if (_deletedNodes.TryGetValue(i, out var spriteNode))
                    layer.InsertFrameFromBitmapNode(_deletedFrameIndex, spriteNode);
                else
                    layer.InsertEmptyFrame(_deletedFrameIndex);
            }

            _sprite.SetFrameIndex(_deletedFrameIndex);
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _sprite.Yield();
        }
    }
}