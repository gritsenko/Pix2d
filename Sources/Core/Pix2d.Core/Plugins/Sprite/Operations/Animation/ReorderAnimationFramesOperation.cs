using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations;

public class ReorderAnimationFramesOperation : EditOperationBase, ISpriteEditorOperation
{
    private readonly Pix2dSprite _sprite;
    private readonly int _oldFrameIndex;
    private readonly int _newFrameIndex;

    public override bool AffectsNodeStructure => true;

    public HashSet<int> AffectedLayerIndexes { get; }
    public HashSet<int> AffectedFrameIndexes { get; }

    public ReorderAnimationFramesOperation(Pix2dSprite sprite, int oldIndex, int newIndex)
    {
            _sprite = sprite;
            _oldFrameIndex = oldIndex;
            _newFrameIndex = newIndex;

            AffectedFrameIndexes = [oldIndex, newIndex];
            AffectedLayerIndexes = sprite.Layers.Select(x => x.Index).ToHashSet();
        //addAfterFrameIndex == -1 means add to end of list
    }

    public override void OnPerform()
    {
            var layers = _sprite.Layers.ToArray();
            var reorderedFrames = _sprite.Layers.Select(x => LayerFrameMeta.Copy(x.Frames[_oldFrameIndex])).ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                var frame = reorderedFrames[i];
                layers[i].Frames.RemoveAt(_oldFrameIndex);
                layers[i].Frames.Insert(_newFrameIndex, frame);
            }

            _sprite.SetFrameIndex(_newFrameIndex);
        }

    public override void OnPerformUndo()
    {
            var layers = _sprite.Layers.ToArray();
            var reorderedFrames = _sprite.Layers.Select(x => LayerFrameMeta.Copy(x.Frames[_newFrameIndex])).ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                var frame = reorderedFrames[i];
                layers[i].Frames.RemoveAt(_newFrameIndex);
                layers[i].Frames.Insert(_oldFrameIndex, frame);
            }
            _sprite.SetFrameIndex(_oldFrameIndex);
        }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
            return _sprite.Yield();
        }
}