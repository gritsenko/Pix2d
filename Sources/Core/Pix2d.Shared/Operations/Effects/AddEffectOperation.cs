using System;
using System.Collections.Generic;
using System.Linq;
using SkiaNodes;

namespace Pix2d.Operations
{
    public class AddEffectOperation : EditOperationBase
    {
        private SKNode[] _nodes;
        private ISKNodeEffect _effect;
        private ISKNodeEffect[] _addedEffects;
        public AddEffectOperation(IEnumerable<SKNode> nodes, ISKNodeEffect effect)
        {
            _effect = effect;
            _nodes = nodes.ToArray();
        }

        public override void OnPerform()
        {
            if (_addedEffects == null)
            {
                _addedEffects = new ISKNodeEffect[_nodes.Length];
                for (var i = 0; i < _nodes.Length; i++)
                {
                    _addedEffects[i] = _effect.Clone();
                }
            }

            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                
                if (node.Effects == null) 
                    node.Effects = new List<ISKNodeEffect>();

                node.Effects.Add(_addedEffects[i]);
            }
        }

        public override void OnPerformUndo()
        {
            if (_addedEffects != null)
            {
                for (var i = 0; i < _nodes.Length; i++)
                {
                    var node = _nodes[i];
                    node.Effects.Remove(_addedEffects[i]);

                    if(node.Effects.Count == 0)
                        node.Effects = null;
                }
            }
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _nodes;
        }

        public ISKNodeEffect GetEffect(SKNode node)
        {
            var index = Array.IndexOf(_nodes, node);
            return _addedEffects[index];
        }
    }
}