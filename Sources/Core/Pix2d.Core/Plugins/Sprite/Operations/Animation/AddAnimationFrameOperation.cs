#nullable enable
using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations;

public class AddAnimationFrameOperation : EditOperationBase, ISpriteEditorOperation
{
    private readonly Pix2dSprite _sprite;
    private readonly int _restoreFrameIndex;
    private readonly int _newFrameIndex;
    private LayerFrameMeta?[]? _framesToRestore;
    private BitmapNode?[]? _nodesToRestore;

    // Animation metadata (tags + per-frame durations) is index-keyed, so inserting a frame re-indexes
    // it. Undo restores the snapshot wholesale rather than inverting the shift — see
    // SpriteAnimationMetaSnapshot for why the inverse doesn't exist.
    private SpriteAnimationMetaSnapshot? _metaSnapshot;

    public override bool AffectsNodeStructure => true;

    public HashSet<int> AffectedLayerIndexes { get; } = [];
    public HashSet<int> AffectedFrameIndexes { get; } = [];

    public int FrameIndex => _newFrameIndex;

    public AddAnimationFrameOperation(Pix2dSprite sprite, int previousIndex)
    {
        _sprite = sprite;

        //previousIndex == -1 means add to end of list
        _newFrameIndex = previousIndex == -1 ? sprite.GetFramesCount() : previousIndex + 1;
        _restoreFrameIndex = previousIndex == -1 ? _newFrameIndex - 1 : previousIndex;

        AffectedFrameIndexes = [_restoreFrameIndex, _newFrameIndex];
        AffectedLayerIndexes = sprite.Layers.Select(x => x.Index).ToHashSet();
    }

     public override void OnPerform()
     {
         // Capture once: redo re-runs this method on state undo just restored, and the shift is
         // deterministic, so re-capturing would overwrite the pre-edit baseline with a post-edit one.
         _metaSnapshot ??= SpriteAnimationMetaSnapshot.Capture(_sprite);

         var layers = _sprite.Layers.ToArray();

         for (var i = 0; i < layers.Length; i++)
         {
             // The snapshot is indexed by the layer order as it stood during undo. A layer that has no
             // entry there — or whose entry is null because it had no frame to give back — gets an empty
             // frame, exactly as it would on a first Perform. See OnPerformUndo for why that happens.
             var restored = _nodesToRestore != null && i < _nodesToRestore.Length
                 ? _nodesToRestore[i]
                 : null;

             if (restored != null)
                 layers[i].InsertFrameFromBitmapNode(_newFrameIndex, restored);
             else
                 layers[i].InsertEmptyFrame(_newFrameIndex);
         }

         // After the frames exist, so the tag clamp sees the new frame count.
         _sprite.ShiftAnimationMetaOnInsert(_newFrameIndex);

         _sprite.SetFrameIndex(_newFrameIndex);
     }

     public override void OnPerformUndo()
     {
         var layers = _sprite.Layers.ToArray();

         _framesToRestore = new LayerFrameMeta[layers.Length];
         _nodesToRestore = new BitmapNode[layers.Length];
         for (var i = 0; i < layers.Length; i++)
         {
             // Not every layer necessarily carries this frame. The layer set is read live here rather
             // than captured at Perform time, and layers come and go between the two — importing an
             // image adds one (ImportAsync → AddLayerOperation, which is exactly what the op log shows
             // before this threw) — so the frame counts can differ from the one this operation inserted
             // into. Indexing Frames blindly turned that into ArgumentOutOfRangeException out of
             // Edit.Undo; a layer with nothing at this index simply has nothing to remove or restore.
             if (_newFrameIndex < 0 || _newFrameIndex >= layers[i].Frames.Count)
                 continue;

             _framesToRestore[i] = layers[i].Frames[_newFrameIndex];
             _nodesToRestore[i] = layers[i].GetSpriteByFrame(_newFrameIndex)!;
             layers[i].DeleteFrame(_newFrameIndex);
         }

         _metaSnapshot?.Restore(_sprite);

         _sprite.SetFrameIndex(_restoreFrameIndex);
     }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _sprite.Yield();
    }
}