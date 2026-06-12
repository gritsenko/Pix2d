using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Operations;
using SkiaNodes;
using System.Runtime.InteropServices;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;

namespace Pix2d.Operations.Drawing;

public class DrawingOperationWithDiffState : EditOperationBase, IDisposable, ISpriteEditorOperation, ICacheableOperation
{
    public record struct DiffBlock(int Len, int OldColor, int NewColor);

    private readonly IDrawingTarget _drawingTarget;

    private CachedPayload<List<DiffBlock>> _changesPayload; // Stores the differences
    private int _frame;
    private int _layerIndex;
    private int _finalFrame;
    private int _finalLayerIndex;
    public HashSet<int> AffectedFrameIndexes { get; } = [];
    public HashSet<int> AffectedLayerIndexes { get; } = [];

    public DrawingOperationWithDiffState(IDrawingTarget drawingTarget, List<DiffBlock> changes)
    {
        _drawingTarget = drawingTarget;
        _changesPayload = new CachedPayload<List<DiffBlock>>(changes, "diff_changes");

        if (_drawingTarget is IAnimatedNode sprite)
        {
            _frame = sprite.CurrentFrameIndex;
            _layerIndex = sprite.SelectedLayerIndex;

            AffectedFrameIndexes = [_frame];
            AffectedLayerIndexes = [_layerIndex];
        }
    }

    public void SetFinalData()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            _finalFrame = sprite.CurrentFrameIndex;
            _finalLayerIndex = sprite.SelectedLayerIndex;

            AffectedFrameIndexes.Add(_finalFrame);
            AffectedLayerIndexes.Add(_finalLayerIndex);
        }
    }

    public override void OnPerform()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            sprite.SelectedLayerIndex = _finalLayerIndex;
            sprite.SetFrameIndex(_finalFrame);
        }

        ApplyChanges(_drawingTarget, _changesPayload.GetValue());
    }

    public override void OnPerformUndo()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            sprite.SelectedLayerIndex = _layerIndex;
            sprite.SetFrameIndex(_frame);
        }

        ApplyChanges(_drawingTarget, _changesPayload.GetValue(), true);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        if (_drawingTarget is SKNode node)
            yield return node;
    }

    public IDrawingTarget GetDrawingTarget() => _drawingTarget;

    public bool HasChanges()
    {
        return _changesPayload.IsEvicted ? true : _changesPayload.GetValue().Count > 0;
    }

    public void Dispose()
    {
        if (!_changesPayload.IsEvicted)
        {
            _changesPayload.GetValue().Clear();
        }
    }

    public bool CanMerge(DrawingOperationWithDiffState operation)
    {
        return _drawingTarget == operation._drawingTarget && _frame == operation._frame &&
            _layerIndex == operation._layerIndex;
    }

    public void Merge(DrawingOperationWithDiffState operation)
    {
        if (!CanMerge(operation))
        {
            throw new InvalidOperationException("Operation drawing targets are not same");
        }

        //_changesPayload.GetValue().AddRange(operation._changesPayload.GetValue());
    }

    public void EvictToDisk(IOperationDiskCacheService cache)
    {
        _changesPayload.EvictToDisk(cache, changes =>
        {
            var span = CollectionsMarshal.AsSpan(changes);
            return MemoryMarshal.AsBytes(span).ToArray();
        });
    }

    public void ClearDiskCache(IOperationDiskCacheService cache)
    {
        _changesPayload.ClearDiskCache(cache);
    }

    public void RestoreFromDisk(IOperationDiskCacheService cache)
    {
        _changesPayload.RestoreFromDisk(cache, bytes =>
        {
            var span = MemoryMarshal.Cast<byte, DiffBlock>(bytes.AsSpan());
            var list = new List<DiffBlock>(span.Length);
            CollectionsMarshal.SetCount(list, span.Length);
            span.CopyTo(CollectionsMarshal.AsSpan(list));
            return list;
        });
    }

    private void ApplyChanges(IDrawingTarget target, List<DiffBlock> changes, bool reverse = false)
    {
        var data = target.GetData();
        var pixels = MemoryMarshal.Cast<byte, int>(data.AsSpan());

        var index = 0;
        foreach (var diffBlock in changes)
        {
            if (diffBlock.OldColor != diffBlock.NewColor)
            {
                var val = reverse ? diffBlock.OldColor : diffBlock.NewColor;
                var len = diffBlock.Len;

                for (var i = 0; i < len; i++)
                {
                    pixels[index + i] = val;
                }
            }
            index += diffBlock.Len;
        }

        target.SetData(data);
    }

}
