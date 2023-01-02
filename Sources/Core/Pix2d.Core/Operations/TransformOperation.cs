using System.Collections.Generic;
using System.Linq;
using Pix2d.Primitives.Operations;
using SkiaNodes;

namespace Pix2d.Operations
{
    public class TransformOperation : EditOperationBase
    {
        private SKNode[] _nodes;
        private SKNodeState[] _initialStates;
        private SKNodeState[] _finalStates;

        protected bool HasFinalStates => _finalStates != null;

        protected SKNode[] Nodes => _nodes;

        public TransformOperation()
        {

        }
        
        public TransformOperation(IEnumerable<SKNode> nodes)
        {
            SetInitialData(nodes);
        }

        public void SetInitialData(IEnumerable<SKNode> nodes)
        {
            _nodes = nodes.ToArray();
            _initialStates = _nodes.GetNodeStates();
        }

        public void SetFinalData()
        {
            _finalStates = _nodes.GetNodeStates();
        }

        public override void OnPerform()
        {
            _finalStates.ApplyStates();
        }

        public override void OnPerformUndo()
        {
            _initialStates.ApplyStates();
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _initialStates.Select(x => x.TargetNode);
        }

    }
}