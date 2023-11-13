using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Operations;
using SkiaNodes;

namespace Pix2d.Operations
{
    /// <summary>
    /// Allows to perform several edit operations sequentially as one operation
    /// </summary>
    public class BulkEditOperation : EditOperationBase
    {
        private readonly List<IEditOperation> _operations = new List<IEditOperation>();

        public bool Empty => !_operations.Any();

        public BulkEditOperation(params IEditOperation[] operations)
        {
            _operations.AddRange(operations);
            var affectedFrames = new HashSet<int>();
            foreach (var operation in _operations)
            {
                if (operation.AffectedFrameIndexes != null)
                {
                    foreach (var frame in operation.AffectedFrameIndexes)
                    {
                        affectedFrames.Add(frame);
                    }
                }
            }

            if (affectedFrames.Count > 0)
            {
                AffectedFrameIndexes = affectedFrames;
            }
        }

        public void AddSubOperation(IEditOperation operation)
        {
            _operations.Add(operation);
        }

        public override bool AffectsNodeStructure => _operations.Any(x => x.AffectsNodeStructure);

        public override void OnPerform()
        {
            foreach (var operation in _operations)
            {
                operation.OnPerform();
            }
        }

        public void Add(IEditOperation operation)
        {
            _operations.Add(operation);
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            return _operations.SelectMany(x => x.GetEditedNodes()).Distinct();
        }

        public override void OnPerformUndo()
        {
            foreach (var operation in _operations.OfType<IEditOperation>().Reverse())
            {
                operation.OnPerformUndo();
            }
        }

        public bool HasOperation(IEditOperation operation)
        {
            return _operations.Any(x => x == operation);
        }
    }
}