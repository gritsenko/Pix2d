using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations;

public class AddAnimationFrameOperation : EditOperationBase, ISpriteEditorOperation
{
    private readonly Pix2dSprite _sprite;
    private readonly int _previousIndex;
    private readonly int _newFrameIndex;
    private LayerFrameMeta[] _framesToRestore;
    private BitmapNode[] _nodesToRestore;

    public override bool AffectsNodeStructure => true;

    public HashSet<int> AffectedLayerIndexes { get; }
    public HashSet<int> AffectedFrameIndexes { get; }

    public int FrameIndex => _newFrameIndex;

    public AddAnimationFrameOperation(Pix2dSprite sprite, int previousIndex)
    {
        _sprite = sprite;

        //previousIndex == -1 means add to end of list
        _previousIndex = previousIndex;
        _newFrameIndex = _previousIndex + 1;

        AffectedFrameIndexes = [_previousIndex, _newFrameIndex];
        AffectedLayerIndexes = sprite.Layers.Select(x => x.Index).ToHashSet();
    }

    public override void OnPerform()
    {
        var layers = _sprite.Layers.ToArray();

        for (var i = 0; i < layers.Length; i++)
        {
            if (_framesToRestore != null)
            {
                layers[i].InsertFrameFromBitmapNode(_newFrameIndex, _nodesToRestore[i]);
            }
            else
            {
                layers[i].InsertEmptyFrame(_newFrameIndex);
            }
        }

        _sprite.SetFrameIndex(_newFrameIndex);
    }

    public override void OnPerformUndo()
    {
        var layers = _sprite.Layers.ToArray();

        _framesToRestore = new LayerFrameMeta[layers.Length];
        _nodesToRestore = new BitmapNode[layers.Length];
        for (var i = 0; i < layers.Length; i++)
        {
            _framesToRestore[i] = layers[i].Frames[_newFrameIndex];
            _nodesToRestore[i] = layers[i].GetSpriteByFrame(_newFrameIndex);
            layers[i].DeleteFrame(_newFrameIndex);
        }

        _sprite.SetFrameIndex(_previousIndex);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _sprite.Yield();
    }
}