using System.Collections.Generic;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class ReorderLayersOperation : EditOperationBase
    {
        private readonly Pix2dSprite _sprite;
        private readonly Pix2dSprite.Layer _reorderedLayer;
        private readonly int _oldLayerIndex;
        private readonly int _newLayerIndex;

        public override bool AffectsNodeStructure => true;

        public ReorderLayersOperation(Pix2dSprite sprite, int oldIndex, int newIndex)
        {
            _sprite = sprite;
            _oldLayerIndex = oldIndex;
            _newLayerIndex = newIndex;
            _reorderedLayer = sprite.Nodes[_oldLayerIndex] as Pix2dSprite.Layer;

            //addAfterFrameIndex == -1 means add to end of list
        }

        public override void OnPerform()
        {
            _sprite.Nodes.Remove(_reorderedLayer);
            _sprite.Nodes.Insert(_newLayerIndex, _reorderedLayer);

            _sprite.SelectLayer(_reorderedLayer);
        }

        public override void OnPerformUndo()
        {
            _sprite.Nodes.Remove(_reorderedLayer);
            _sprite.Nodes.Insert(_oldLayerIndex, _reorderedLayer);

            _sprite.SelectLayer(_reorderedLayer);
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _sprite.Yield();
        }
    }
}