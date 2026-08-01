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

    // Undo re-runs Reorder in the opposite direction for the frames, but the metadata shift is not
    // symmetric (a single-frame tag can be rescued, a range can absorb the move), so the metadata is
    // restored from a pre-edit snapshot instead. See SpriteAnimationMetaSnapshot.
    private SpriteAnimationMetaSnapshot? _metaSnapshot;

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
            _metaSnapshot ??= SpriteAnimationMetaSnapshot.Capture(_sprite);
            Reorder(_oldFrameIndex, _newFrameIndex, shiftMeta: true);
        }

    public override void OnPerformUndo()
    {
            Reorder(_newFrameIndex, _oldFrameIndex, shiftMeta: false);
            _metaSnapshot?.Restore(_sprite);
        }

    private void Reorder(int fromIndex, int toIndex, bool shiftMeta)
    {
            var layers = _sprite.Layers.ToArray();

            // Validate both indices against EVERY layer before touching any of them. Layers can hold
            // different frame counts (e.g. a prior partial reorder), and mutating layer-by-layer while
            // one throws mid-loop would leave layers with mismatched counts and corrupt the project.
            foreach (var layer in layers)
            {
                var count = layer.Frames.Count;
                if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count)
                    return;
            }

            var reorderedFrames = layers.Select(x => LayerFrameMeta.Copy(x.Frames[fromIndex])).ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                var frame = reorderedFrames[i];
                layers[i].Frames.RemoveAt(fromIndex);
                layers[i].Frames.Insert(toIndex, frame);
            }

            // Only past the validation above — a bailed-out reorder must leave the metadata alone or
            // tags/durations desync from frames that never moved.
            if (shiftMeta)
                _sprite.ShiftAnimationMetaOnMove(fromIndex, toIndex);

            _sprite.SetFrameIndex(toIndex);
        }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
            return _sprite.Yield();
        }
}