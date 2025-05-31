using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Operations;
using SkiaNodes;

namespace Pix2d.Operations.Drawing;

public class DrawingOperationWithFullState : EditOperationBase, IDisposable, ISpriteEditorOperation
{
    private readonly IDrawingTarget _drawingTarget;

    private byte[] _initialData;
    private byte[] _finalData;
    private int _frame;
    private int _layerIndex;
    private int _finalFrame;
    private int _finalLayerIndex;

    public HashSet<int> AffectedFrameIndexes { get; private set; }
    public HashSet<int> AffectedLayerIndexes { get; private set; }

    public DrawingOperationWithFullState(IDrawingTarget drawingTarget)
    {
        _drawingTarget = drawingTarget;
    }

    public void SetInitialData(byte[] initialData)
    {
        _initialData = initialData;

        if (_drawingTarget is IAnimatedNode sprite)
        {
            _frame = sprite.CurrentFrameIndex;
            _layerIndex = sprite.SelectedLayerIndex;

            AffectedFrameIndexes = [_frame, _finalFrame];
            AffectedLayerIndexes = [_layerIndex, _finalLayerIndex];
        }
    }

    public void SetFinalData(byte[] finalData)
    {
        _finalData = finalData;
        if (_finalData != null && _finalData[0] == 0)
        {
            // Debugger.Break();
        }

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

        _drawingTarget.SetData(_finalData);
    }

    public override void OnPerformUndo()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            sprite.SelectedLayerIndex = _layerIndex;
            sprite.SetFrameIndex(_frame);
        }

        _drawingTarget.SetData(_initialData);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingTarget as SKNode;
    }

    public IDrawingTarget GetDrawingTarget() => _drawingTarget;

    public bool HasChanges()
    {
        if (_finalData == null)
        {
            return _initialData != null;
        }

        if (_initialData == null)
        {
            return true;
        }

        return !((ReadOnlySpan<byte>)_finalData).SequenceEqual((ReadOnlySpan<byte>)_initialData);
    }

    public void Dispose()
    {
        _initialData = null;
        _finalData = null;
    }

    public bool CanMerge(DrawingOperationWithFullState operation)
    {
        return _drawingTarget == operation._drawingTarget && _frame == operation._frame &&
            _layerIndex == operation._layerIndex;
    }

    public void Merge(DrawingOperationWithFullState operation)
    {
        if (!CanMerge(operation))
        {
            throw new InvalidOperationException("Operation drawing targets are not same");
        }

        _finalData = operation._finalData;
    }
}