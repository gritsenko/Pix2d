using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Operations;
using SkiaNodes;
using System.Runtime.InteropServices;
using Pix2d.Abstract.Operations;

public class DrawingOperationWithDiffState : EditOperationBase, IDisposable, ISpriteEditorOperation
{
    public record struct DiffBlock(int Len, int OldColor, int NewColor);

    private readonly IDrawingTarget _drawingTarget;

    private List<DiffBlock> _changes; // Stores the differences
    private int _frame;
    private int _layerIndex;
    private int _finalFrame;
    private int _finalLayerIndex;
    private readonly byte[] _initialData;
    public HashSet<int> AffectedFrameIndexes { get; }
    public HashSet<int> AffectedLayerIndexes { get; }

    public DrawingOperationWithDiffState(IDrawingTarget drawingTarget, List<DiffBlock> changes)
    {
        _drawingTarget = drawingTarget;
        _changes = changes;

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

        ApplyChanges(_drawingTarget, _changes);
    }

    public override void OnPerformUndo()
    {
        if (_drawingTarget is IAnimatedNode sprite)
        {
            sprite.SelectedLayerIndex = _layerIndex;
            sprite.SetFrameIndex(_frame);
        }

        ApplyChanges(_drawingTarget, _changes, true);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingTarget as SKNode;
    }

    public IDrawingTarget GetDrawingTarget() => _drawingTarget;

    public bool HasChanges()
    {
        return _changes != null && _changes.Count > 0;
    }

    public void Dispose()
    {
        _changes = null;
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

        //_changes.AddRange(operation._changes);
    }


    private void ApplyChanges(IDrawingTarget target, List<DiffBlock> changes, bool reverse = false)
    {
        var data = target.GetData();
        var pixels = MemoryMarshal.Cast<byte, int>(data);

        var index = 0;
        foreach (var diffBlock in changes)
        {
            var p0 = diffBlock.OldColor;
            var p1 = diffBlock.NewColor;
            if (p0 != p1)
            {

                var val = reverse ? p0 : p1;

                for (var i = 0; i < diffBlock.Len; i++)
                {
                    pixels[index + i] = val;
                }
            }
            index += diffBlock.Len;
        }


        target.SetData(data);
    }

}
