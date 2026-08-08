#nullable enable
using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations;

/// <summary>
/// Links a set of the selected layer's frames onto one shared image, or unlinks a single frame back onto its
/// own copy (roadmap H2.4 — linked cels).
///
/// Linking is **per layer**, matching what a cel actually is: a static background layer can share one image
/// across the whole animation while the layers above it keep animating. It is also destructive — the followers'
/// own sprite nodes are dropped — which is why undo restores a *snapshot* of the layer's frame table and node
/// set rather than trying to invert the change. Inverting is the trap the animation-metadata work already hit:
/// a dropped bitmap cannot be recomputed, and hand-written inverse index arithmetic is what produced the 3.11.1
/// timeline crashes.
/// </summary>
public class LinkAnimationFramesOperation : EditOperationBase, ISpriteEditorOperation
{
    private readonly Pix2dSprite _sprite;
    private readonly int _layerIndex;
    private readonly int[] _frameIndices;
    private readonly int _sourceFrameIndex;
    private readonly bool _link;

    private LayerSnapshot? _snapshot;

    public override bool AffectsNodeStructure => true;

    public HashSet<int> AffectedLayerIndexes { get; }
    public HashSet<int> AffectedFrameIndexes { get; }

    /// <param name="frameIndices">Frames to link. Ignored when <paramref name="link"/> is false.</param>
    /// <param name="sourceFrameIndex">
    /// The frame whose pixels survive a link, and the frame to detach when unlinking.
    /// </param>
    public LinkAnimationFramesOperation(Pix2dSprite sprite, int layerIndex, IReadOnlyList<int> frameIndices,
        int sourceFrameIndex, bool link)
    {
        _sprite = sprite;
        _layerIndex = layerIndex;
        _frameIndices = frameIndices.Distinct().OrderBy(i => i).ToArray();
        _sourceFrameIndex = sourceFrameIndex;
        _link = link;

        AffectedLayerIndexes = [layerIndex];
        AffectedFrameIndexes = [.. _frameIndices, sourceFrameIndex];
    }

    /// <summary>True when the operation would actually change something — checked before it is pushed.</summary>
    public bool IsApplicable()
    {
        var layer = GetLayer();
        if (layer == null)
            return false;

        if (!_link)
            return layer.IsFrameLinked(_sourceFrameIndex);

        if (_frameIndices.Length < 2
            || !_frameIndices.Contains(_sourceFrameIndex)
            || !_frameIndices.All(i => i >= 0 && i < layer.FrameCount))
        {
            return false;
        }

        // Re-linking an already identical group would push an undo step that restores the state it started
        // from — the "lost click" on Ctrl+Z that the sibling operations are careful to avoid. Reachable by
        // simply invoking LinkAllFrames twice.
        var sourceNodeId = layer.Frames[_sourceFrameIndex].NodeId;
        var alreadyLinked = sourceNodeId != Guid.Empty
                            && _frameIndices.All(i => layer.Frames[i] is { IsLinked: true } f && f.NodeId == sourceNodeId);

        return !alreadyLinked;
    }

    public override void OnPerform()
    {
        var layer = GetLayer();
        if (layer == null)
            return;

        // Captured once: redo re-runs this on the state undo just restored, so re-capturing here would
        // overwrite the pre-edit baseline with a post-edit one (same reasoning as the sibling operations).
        _snapshot ??= LayerSnapshot.Capture(layer);

        if (_link)
            layer.LinkFrames(_frameIndices, _sourceFrameIndex);
        else
            layer.UnlinkFrame(_sourceFrameIndex);

        _sprite.SetFrameIndex(_sourceFrameIndex);
    }

    public override void OnPerformUndo()
    {
        var layer = GetLayer();
        if (layer == null)
            return;

        _snapshot?.Restore(layer);
        _sprite.SetFrameIndex(_sourceFrameIndex);
    }

    public override IEnumerable<SKNode> GetEditedNodes() => _sprite.Yield();

    private Pix2dSprite.Layer? GetLayer()
    {
        var layers = _sprite.Layers.ToArray();
        return _layerIndex >= 0 && _layerIndex < layers.Length ? layers[_layerIndex] : null;
    }

    /// <summary>
    /// A layer's frame table plus the sprite nodes it owned, enough to put both back exactly as they were.
    /// Holds node <i>references</i>, not copies: linking removes a node from the tree without destroying it,
    /// so undo only has to re-attach it — and unlinking adds one, which undo detaches by absence from here.
    /// </summary>
    private sealed class LayerSnapshot
    {
        private readonly LayerFrameMeta[] _frames = [];
        private readonly SpriteNode[] _nodes = [];

        private LayerSnapshot() { }

        private LayerSnapshot(LayerFrameMeta[] frames, SpriteNode[] nodes)
        {
            _frames = frames;
            _nodes = nodes;
        }

        public static LayerSnapshot Capture(Pix2dSprite.Layer layer) =>
            new(layer.Frames.Select(LayerFrameMeta.Copy).ToArray(),
                layer.Nodes.OfType<SpriteNode>().ToArray());

        public void Restore(Pix2dSprite.Layer layer)
        {
            // Nodes this operation created (an unlink's private copy) are not in the snapshot — drop them.
            foreach (var node in layer.Nodes.OfType<SpriteNode>().Where(n => !_nodes.Contains(n)).ToArray())
                node.RemoveFromParent();

            // Nodes it removed (a link's discarded followers) are in the snapshot but no longer attached.
            foreach (var node in _nodes.Where(n => !layer.Nodes.Contains(n)))
                layer.Nodes.Add(node);

            // Frame count never changes here, but restore defensively rather than assuming it.
            layer.Frames.Clear();
            foreach (var frame in _frames)
                layer.Frames.Add(LayerFrameMeta.Copy(frame));
        }
    }
}
