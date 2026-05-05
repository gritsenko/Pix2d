using System;
using System.Collections.Generic;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using SkiaNodes;

namespace Pix2d.Operations.Drawing;

public class DrawingOperationWithFullState : EditOperationBase, IDisposable, ISpriteEditorOperation, ICacheableOperation
{
    private readonly IDrawingTarget _drawingTarget = null!;

    private CachedPayload<byte[]>? _initialPayload;
    private CachedPayload<byte[]>? _finalPayload;
    private int _frame;
    private int _layerIndex;
    private int _finalFrame;
    private int _finalLayerIndex;

    public HashSet<int> AffectedFrameIndexes { get; private set; } = new();
    public HashSet<int> AffectedLayerIndexes { get; private set; } = new();

    public DrawingOperationWithFullState(IDrawingTarget drawingTarget)
    {
        _drawingTarget = drawingTarget;
    }

    public void SetInitialData(byte[]? initialData)
    {
        _initialPayload = initialData != null ? new CachedPayload<byte[]>(initialData, "full_initial") : null;

        if (_drawingTarget is IAnimatedNode sprite)
        {
            _frame = sprite.CurrentFrameIndex;
            _layerIndex = sprite.SelectedLayerIndex;

            AffectedFrameIndexes = [_frame, _finalFrame];
            AffectedLayerIndexes = [_layerIndex, _finalLayerIndex];
        }
    }

    public void SetFinalData(byte[]? finalData)
    {
        _finalPayload = finalData != null ? new CachedPayload<byte[]>(finalData, "full_final") : null;

        if (_drawingTarget is IAnimatedNode sprite)
        {
            _finalFrame = sprite.CurrentFrameIndex;
            _finalLayerIndex = sprite.SelectedLayerIndex;

            AffectedFrameIndexes = [_frame, _finalFrame];
            AffectedLayerIndexes = [_layerIndex, _finalLayerIndex];
        }
    }

    public override void OnPerform()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            sprite.SelectedLayerIndex = _finalLayerIndex;
            sprite.SetFrameIndex(_finalFrame);
        }

        // Only restore if we have valid final data (null means no data captured)
        if (_finalPayload != null)
        {
            _drawingTarget.SetData(_finalPayload.GetValue());
        }
    }

    public override void OnPerformUndo()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            sprite.SelectedLayerIndex = _layerIndex;
            sprite.SetFrameIndex(_frame);
        }

        // Only restore if we have valid initial data (null means no data captured)
        if (_initialPayload != null)
        {
            _drawingTarget.SetData(_initialPayload.GetValue());
        }
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        if (_drawingTarget is SKNode node)
        {
            yield return node;
        }
    }

    public IDrawingTarget GetDrawingTarget() => _drawingTarget;

    public bool HasChanges()
    {
        if (_finalPayload == null)
        {
            return _initialPayload != null;
        }

        if (_initialPayload == null)
        {
            return true;
        }

        if (_initialPayload.IsEvicted || _finalPayload.IsEvicted) throw new InvalidOperationException("Cannot compare evicted data.");

        return !((ReadOnlySpan<byte>)_finalPayload.GetValue()).SequenceEqual((ReadOnlySpan<byte>)_initialPayload.GetValue());
    }

    public void Dispose()
    {
        _initialPayload = null;
        _finalPayload = null;
    }

    public bool CanMerge(DrawingOperationWithFullState? operation)
    {
        return operation != null && _drawingTarget == operation._drawingTarget && _frame == operation._frame &&
            _layerIndex == operation._layerIndex;
    }

    public void Merge(DrawingOperationWithFullState operation)
    {
        if (!CanMerge(operation))
        {
            throw new InvalidOperationException("Operation drawing targets are not same");
        }

        _finalPayload = operation._finalPayload;
    }

    public void EvictToDisk(IOperationDiskCacheService cache)
    {
        _initialPayload?.EvictToDisk(cache, b => b);
        _finalPayload?.EvictToDisk(cache, b => b);
    }

    public void RestoreFromDisk(IOperationDiskCacheService cache)
    {
        _initialPayload?.RestoreFromDisk(cache, b => b);
        _finalPayload?.RestoreFromDisk(cache, b => b);
    }
}