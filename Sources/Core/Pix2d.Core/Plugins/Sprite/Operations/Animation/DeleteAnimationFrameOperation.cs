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

    // Per layer, the full frame metadata as it stood immediately before the delete. Keyed by layer because a
    // single field could only hold the LAST layer's value: with linked cels (or any duplicate-without-edit)
    // several layers take the "shared node" path on the same delete, so one layer's node id would overwrite
    // another's and undo would hand layer A the id of layer B's node — no node of that id in A, so A's frame
    // came back blank. Storing the whole meta rather than just the id is what also preserves IsLinked.
    private readonly Dictionary<int, LayerFrameMeta> _deletedMetas = new Dictionary<int, LayerFrameMeta>();

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

                // Snapshot before the delete: DeleteFrame may collapse the link group it belonged to, so the
                // meta has to be read while it still describes the frame as the user had it.
                _deletedMetas[i1] = LayerFrameMeta.Copy(layer.Frames[_deletedFrameIndex]);

                layer.DeleteFrame(_deletedFrameIndex, s => _deletedNodes[i1] = s);
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

                if (!_deletedMetas.TryGetValue(i, out var meta))
                    continue;

                // The captured node is passed only when this layer's frame owned it outright; a shared frame
                // restores against the node its meta already names, which its siblings kept attached.
                _deletedNodes.TryGetValue(i, out var spriteNode);
                layer.InsertFrameFromMeta(_deletedFrameIndex, meta, spriteNode as SpriteNode);
            }

            _metaSnapshot?.Restore(_sprite);

            _sprite.SetFrameIndex(_deletedFrameIndex);
        }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
            return _sprite.Yield();
        }
}