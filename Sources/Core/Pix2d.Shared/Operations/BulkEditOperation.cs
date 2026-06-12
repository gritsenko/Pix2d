using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using SkiaNodes;

namespace Pix2d.Operations;

/// <summary>
/// Allows to perform several edit operations sequentially as one operation
/// </summary>
public class BulkEditOperation : EditOperationBase, ISpriteEditorOperation, ICacheableOperation
{
    private readonly List<IEditOperation> _operations = new List<IEditOperation>();

    public IReadOnlyList<IEditOperation> Operations => _operations;

    public bool Empty => !_operations.Any();

    public HashSet<int> AffectedFrameIndexes { get; private set; } = new();
    public HashSet<int> AffectedLayerIndexes { get; private set; } = new();

    public BulkEditOperation(params IEditOperation[] operations)
    {
        _operations.AddRange(operations);
        var affectedFrames = new HashSet<int>();
        foreach (var operation in _operations.OfType<ISpriteEditorOperation>())
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

    public void EvictToDisk(IOperationDiskCacheService cache)
    {
        foreach (var op in _operations.OfType<ICacheableOperation>())
        {
            op.EvictToDisk(cache);
        }
    }

    public void RestoreFromDisk(IOperationDiskCacheService cache)
    {
        foreach (var op in _operations.OfType<ICacheableOperation>())
        {
            op.RestoreFromDisk(cache);
        }
    }

    public void ClearDiskCache(IOperationDiskCacheService cache)
    {
        foreach (var op in _operations.OfType<ICacheableOperation>())
        {
            op.ClearDiskCache(cache);
        }
    }
}