using System.Collections.Generic;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Operations
{
    public class RemoveEffectOperation : EditOperationBase
    {
        private SKNode _node;
        private ISKNodeEffect _effect;
        public RemoveEffectOperation(SKNode node, ISKNodeEffect effect)
        {
            _effect = effect;
            _node = node;
        }

        public override void OnPerform()
        {
            _node.Effects.Remove(_effect);
            if (_node.Effects.Count == 0)
                _node.Effects = null;
        }

        public override void OnPerformUndo()
        {
            if (_node.Effects == null)
                _node.Effects = new List<ISKNodeEffect>();

            _node.Effects.Add(_effect);
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _node.Yield();
        }
    }
}