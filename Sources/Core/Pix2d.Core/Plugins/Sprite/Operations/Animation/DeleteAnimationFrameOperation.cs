using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations;

public class DeleteAnimationFrameOperation : EditOperationBase, ISpriteEditorOperation
{
    private readonly Pix2dSprite _sprite;
    private readonly Dictionary<int, BitmapNode> _deletedNodes = new Dictionary<int, BitmapNode>();
    private readonly int _deletedFrameIndex;
    private readonly int _newFrameIndex;
    private Guid _deletedFrameNodeId;

    // Deleting a frame drops any tag that covered only it and discards that frame's duration override;
    // neither is recomputable, so undo restores a pre-edit snapshot instead of inverting the shift.
    private SpriteAnimationMetaSnapshot? _metaSnapshot;

    public override bool AffectsNodeStructure => true;

    public int FrameIndex => _deletedFrameIndex;

    public HashSet<int> AffectedLayerIndexes { get; }
    public HashSet<int> AffectedFrameIndexes { get; }

    public DeleteAnimationFrameOperation(Pix2dSprite sprite, int frameIndex)
    {
            _sprite = sprite;

            //addAfterFrameIndex == -1 means add to end of list
            _deletedFrameIndex = frameIndex;
            _newFrameIndex = Math.Max(0,_deletedFrameIndex - 1);

            AffectedFrameIndexes = [_deletedFrameIndex, _newFrameIndex];
            AffectedLayerIndexes = sprite.Layers.Select(x => x.Index).ToHashSet();
    }

    public override void OnPerform()
    {
            var layers = _sprite.Layers.ToArray();

            // Layers can desync in frame count (e.g. after an interrupted reorder); Layer.DeleteFrame
            // indexes into each layer's own Frames list, so an index valid for one layer can be out of
            // range for another. Validate against every layer up front so we never delete from some
            // layers and then throw on another, which would leave the sprite inconsistent.
            foreach (var layer in layers)
            {
                if (_deletedFrameIndex < 0 || _deletedFrameIndex >= layer.Frames.Count)
                    return;
            }

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];

                var i1 = i;//resharper idea
     layer.DeleteFrame(_deletedFrameIndex, s => _deletedNodes[i1] = s, f => _deletedFrameNodeId = f);
            }

            // Only after the validation above let the frames actually go: shifting the metadata on a
            // run that bailed out would desync tags/durations from frames that never moved.
            _metaSnapshot ??= SpriteAnimationMetaSnapshot.Capture(_sprite);
            _sprite.ShiftAnimationMetaOnDelete(_deletedFrameIndex);

            _sprite.SetFrameIndex(_newFrameIndex);
        }

    public override void OnPerformUndo()
    {
            var layers = _sprite.Layers.ToArray();

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];

                if (_deletedNodes.TryGetValue(i, out var spriteNode))
                    layer.InsertFrameFromBitmapNode(_deletedFrameIndex, spriteNode);
                else
                    layer.InsertFrameFromNodeId(_deletedFrameIndex, _deletedFrameNodeId);
            }

            _metaSnapshot?.Restore(_sprite);

            _sprite.SetFrameIndex(_deletedFrameIndex);
        }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
            return _sprite.Yield();
        }
}