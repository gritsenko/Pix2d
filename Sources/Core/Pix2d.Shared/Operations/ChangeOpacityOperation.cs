using System.Collections.Generic;
using System.Linq;
using SkiaNodes;

namespace Pix2d.Operations
{
    public class ChangeOpacityOperation : EditOperationBase
    {
        private SKNode[] _nodes;
        private float[] _initialOpacities;
        private float[] _finalOpacities;
        public ChangeOpacityOperation(IEnumerable<SKNode> nodes)
        {
            SetInitialData(nodes);
        }

        public void SetInitialData(IEnumerable<SKNode> nodes)
        {
            _nodes = nodes.ToArray();
            _initialOpacities = _nodes.Select(x => x.Opacity).ToArray();
        }
        public void SetFinalData()
        {
            _finalOpacities = _nodes.Select(x => x.Opacity).ToArray();
        }

        public override void OnPerform()
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                node.Opacity = _finalOpacities[i];
            }
        }

        public override void OnPerformUndo()
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                node.Opacity = _initialOpacities[i];
            }
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _nodes;
        }
    }
}