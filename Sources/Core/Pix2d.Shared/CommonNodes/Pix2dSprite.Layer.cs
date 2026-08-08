#nullable enable
using Newtonsoft.Json;
using Pix2d.Primitives.Edit;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class LayerFrameMeta
{
    /// <summary>
    /// Don't use it outside of Layer class
    /// </summary>
    [JsonProperty("i")] public int NodeIndex { get; set; } = -1;

    [JsonProperty("fid")] public Guid NodeId { get; set; }

    [JsonProperty("k")] public bool IsKeyFrame { get; set; }

    /// <summary>
    /// True when this frame shares its pixels with its siblings <i>on purpose</i> — a linked cel. Several
    /// frames pointing at one <see cref="NodeId"/> is how sharing has always been represented, but until
    /// linked cels that sharing was only ever an unobservable memory optimisation: <c>DuplicateFrame</c>
    /// hands the copy the source's node and the first edit silently splits it off
    /// (<see cref="Pix2dSprite.Layer.EnsureFrameHasUniqueSprite"/>, copy-on-write).
    ///
    /// This flag is what separates the two, and it is the reason the feature needs a flag at all rather than
    /// simply treating every shared node as linked: an existing project saved after a duplicate-without-edit
    /// already contains shared nodes, and reinterpreting those as links would make editing one frame of an
    /// old file silently change another. Absent in older files, so it deserialises to <c>false</c> and those
    /// keep copy-on-write.
    ///
    /// Invariant maintained by <see cref="Pix2dSprite.Layer"/>: the frames sharing one node are either all
    /// linked or all unlinked — never a mix.
    /// </summary>
    [JsonProperty("ln")] public bool IsLinked { get; set; }

    [JsonIgnore] public bool IsEmpty => NodeIndex == -1 && NodeId == Guid.Empty;

    public override string ToString()
    {
        return $"{NodeIndex} : {NodeId} : kf{(IsLinked ? " : linked" : "")}";
    }

    public static LayerFrameMeta Copy(LayerFrameMeta other)
    {
        return new LayerFrameMeta()
        {
            IsKeyFrame = other.IsKeyFrame,
            NodeIndex = other.NodeIndex,
            NodeId = other.NodeId,
            IsLinked = other.IsLinked
        };
    }
}

public partial class Pix2dSprite
{
    public int LayersCount => Nodes.Count;

    public class Layer : SKNode
    {
        private int CurrentFrameIndex { get; set; }

        public List<LayerFrameMeta> Frames { get; set; } = [];

        public int FrameCount => Frames.Count;

        public bool LockTransparentPixels { get; set; }

        public Layer()
        {
            //don't allow layer to be selected by click, so Pix2dSprite will be selected
            //todo: replace with more smart selection
            DesignerState.IsLocked = true;
        }

        public Layer(SKSize size, int framesCount) : this()
        {
            Size = size;
            InitFrames(framesCount);
        }

        public Layer Copy()
        {
            var copy = this.Clone() as Layer ?? throw new InvalidOperationException("Clone did not return a Layer");
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (i < copy.Nodes.Count && copy.Nodes[i] is SpriteNode sprite)
                {
                    sprite.Bitmap = sprite.Bitmap?.Copy();
                }
            }

            return copy;
        }

        private SpriteNode? GetActiveFrameSprite() => GetSpriteByFrame(GetFrameByIndex(CurrentFrameIndex));
        public SpriteNode? GetSpriteByFrame(int index) => GetSpriteByFrame(GetFrameByIndex(index));

        /// <summary>
        /// The raw pixels of the frame this layer is currently showing, or null when the frame has no
        /// bitmap yet. Read-only use only — this is the live buffer, not a copy.
        /// </summary>
        public SKBitmap? GetCurrentFrameBitmap() => GetActiveFrameSprite()?.Bitmap;

        /// <summary>
        /// Resolves frame metadata by index, or null when the index doesn't address an existing frame.
        /// Frame indexes reach this class from UI collections (timeline VMs), undo operations and the
        /// playback timer, and those can lag one edit behind the model — a frame deleted while the
        /// timeline still points at it used to surface as a fatal ArgumentOutOfRangeException from
        /// Frames[index] on the next brush stroke. Layers can also desync in frame count, so an index
        /// valid for one layer may be out of range for another. Degrading to null lets callers skip the
        /// operation instead of killing the stroke.
        /// </summary>
        private LayerFrameMeta? GetFrameByIndex(int index)
        {
            EnsureFramesInitialized();

            if (index < 0 || index >= Frames.Count)
                return null;

            return Frames[index];
        }

        /// <summary>
        /// If project was saved in legacy versions of pix2d, there was no info about frames and each frame was a single node in child nodes collection
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void EnsureFramesInitialized()
        {
            if(Frames.Count > 0) return;

            if (Frames.Count == 0 && Nodes.Count > 0)
            {
                for (var i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i] is BitmapNode bitmapNode)
                        InsertFrameFromBitmapNode(i, bitmapNode);
                }
            }
        }

        private SpriteNode? GetSpriteByFrame(LayerFrameMeta? frame)
        {
            return frame == null || frame.NodeId == Guid.Empty
                ? null
                : Nodes.OfType<SpriteNode>().FirstOrDefault(x => x.Id == frame.NodeId);
        }

        private void InitFrames(int framesCount)
        {
            Frames = [];
            for (var i = 0; i < framesCount; i++)
            {
                AddEmptyFrame();
            }
        }

        protected override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            // Onion skin is an editor-only aid, not content. RenderAdorners is true only for the
            // interactive editor viewport and false for every preview/export/thumbnail/clipboard/
            // project-pack render, so gating on it keeps the previous-frame ghost out of exports
            // (issue #230) — the same convention used for EditMode highlights and artboard labels.
            if (vp.Settings.RenderAdorners
                && Parent is Pix2dSprite parentSprite && parentSprite.OnionSkinSettings.IsEnabled)
            {
                var prevIndex = CurrentFrameIndex - 1;
                if (prevIndex < 0)
                    prevIndex = FrameCount - 1;
                if (GetSpriteByFrame(prevIndex) is SKNode prevFrame)
                    SKNodeRenderer.RenderInCurrentTransform(prevFrame, new RenderContext(canvas, vp, 0.3f));
            }

            if (HiddenFrames.Contains(CurrentFrameIndex)
                || GetActiveFrameSprite() is not SKNode node)
                return;
            // Render the active frame preserving the ancestor transform (scene → sprite → layer). Using the
            // matrix-resetting SKNodeRenderer.Render here would paint the frame at world origin and only look
            // correct while the sprite sits at (0,0) — off-origin artboards would draw onto the first one.
            SKNodeRenderer.RenderInCurrentTransform(node, new RenderContext(canvas, vp));
        }

        protected override void OnChildrenAdded(IEnumerable<SKNode> newNodes)
        {
            base.OnChildrenAdded(newNodes);
            foreach (var newNode in newNodes)
                newNode.IsVisible = false;

            InvalidateBoundingBoxFromContent();
        }

        public void InvalidateBoundingBoxFromContent()
        {
            var bbox = GetBoundingBoxWithContent();
            this.Size = bbox.Size;
            //this.Position = bbox.Location;
            OnNodeInvalidated();
        }

        public void SetData(int index, byte[] data)
        {
            var sprite = GetSpriteByFrame(index);

            if (data == null || data.Length == 0 || sprite == null)
            {
                ClearFrame(index);
                return;
            }

            sprite.SetData(data);
        }

        internal void EnsureFrameHasUniqueSprite(int frameIndex)
        {
            //frameIndex = 999;
            try
            {

                var frame = GetFrameByIndex(frameIndex);
                if (frame == null || HasFrameUniqueSprite(frame))
                    return;

                // A deliberately linked cel must NOT be split off by an edit — sharing the pixels is the
                // whole point, so writing through to the shared node is what makes every linked frame update
                // together. This single early-return is what turns the pre-existing copy-on-write sharing
                // into the linked-cel feature; unlinking is an explicit action (UnlinkFrame) instead.
                if (frame.IsLinked)
                    return;

                var sprite = new SpriteNode(this.Size);

                if (!frame.IsEmpty) //copy data from old sprite if the the frame wasn't empty
                {
                    var srcSprite = GetSpriteByFrame(frame);
                    var srcData = srcSprite?.GetData();
                    if (srcData != null)
                        sprite.SetData(srcData);
                    if (srcSprite != null)
                        sprite.TakeBitmapSubstitute(srcSprite);
                }

                sprite.DesignerState.IsLocked = true;
                sprite.Position = SKPoint.Empty;

                SetSpriteToFrame(frame, sprite);
            }
            catch (Exception e)
            {
                var ex = new Exception(
                    $"Frame with index {frameIndex} doesn't exist. Frames count: {Frames.Count}", e);
                Logger.LogException(ex);
            }
        }

        /// <summary>
        /// True when this frame is a linked cel — its pixels are shared with at least one other frame on
        /// purpose, so editing it edits them all.
        /// </summary>
        public bool IsFrameLinked(int frameIndex) => GetFrameByIndex(frameIndex)?.IsLinked == true;

        /// <summary>
        /// The indices of every frame that shares this frame's pixels as a link, including the frame itself.
        /// A frame that is not linked yields just itself, so callers can treat the result uniformly.
        /// </summary>
        public IReadOnlyList<int> GetLinkedFrameIndices(int frameIndex)
        {
            var frame = GetFrameByIndex(frameIndex);
            if (frame == null)
                return [];

            if (!frame.IsLinked || frame.NodeId == Guid.Empty)
                return [frameIndex];

            var result = new List<int>();
            for (var i = 0; i < FrameCount; i++)
            {
                var other = GetFrameByIndex(i);
                if (other != null && other.IsLinked && other.NodeId == frame.NodeId)
                    result.Add(i);
            }

            return result;
        }

        /// <summary>
        /// Makes every frame in <paramref name="frameIndices"/> share one set of pixels. The pixels kept are
        /// the ones of <paramref name="sourceFrameIndex"/> (which must be in the set) — a link has to pick a
        /// winner, and leaving that to the caller is what lets the command say "link to the current frame".
        /// The other frames' own sprite nodes are dropped, so this is destructive and belongs behind an
        /// undoable operation.
        ///
        /// If the source frame is already part of a link, the new frames join that existing link rather than
        /// forming a second one — they are asked to share the source's pixels, and those pixels already have
        /// other holders.
        /// </summary>
        /// <returns>True when at least two frames ended up sharing pixels.</returns>
        public bool LinkFrames(IReadOnlyList<int> frameIndices, int sourceFrameIndex)
        {
            var distinct = frameIndices.Distinct().Where(i => i >= 0 && i < FrameCount).OrderBy(i => i).ToArray();
            if (distinct.Length < 2 || !distinct.Contains(sourceFrameIndex))
                return false;

            // The source needs pixels of its own before anything points at them: linking onto an empty frame
            // would share Guid.Empty, which is "no node" rather than a shared one.
            EnsureFrameHasOwnSprite(sourceFrameIndex);

            var source = GetFrameByIndex(sourceFrameIndex);
            var sourceSprite = GetSpriteByFrame(source);
            if (source == null || sourceSprite == null)
                return false;

            foreach (var index in distinct)
            {
                var frame = GetFrameByIndex(index);
                if (frame == null || ReferenceEquals(frame, source))
                    continue;

                // Drop the frame's own pixels only if nothing else still needs them. A frame already part of
                // another link keeps that link's node alive for its remaining members.
                if (frame.NodeId != source.NodeId && HasFrameUniqueSprite(frame))
                    GetSpriteByFrame(frame)?.RemoveFromParent();

                var formerGroup = frame.IsLinked ? frame.NodeId : Guid.Empty;

                frame.NodeId = source.NodeId;
                frame.NodeIndex = -1;
                frame.IsLinked = true;

                // Pulling a follower out of a *different* group can leave that group with one member. Only
                // reachable through the public LinkFrames(subset) API — LinkAllFrames covers every frame —
                // but SpriteEditor.LinkFrames exposes it for a future range-selection UI.
                if (formerGroup != source.NodeId)
                    CollapseLinkGroupIfSingle(formerGroup);
            }

            source.IsLinked = true;
            return true;
        }

        /// <summary>
        /// Breaks one frame out of its link, giving it a private copy of the shared pixels. The remaining
        /// members stay linked to each other; when only one is left, it stops being a link at all — a
        /// "linked" cel with no siblings would keep drawing the marker and block copy-on-write for nothing.
        /// </summary>
        public bool UnlinkFrame(int frameIndex)
        {
            var frame = GetFrameByIndex(frameIndex);
            if (frame is not { IsLinked: true })
                return false;

            var siblings = GetLinkedFrameIndices(frameIndex).Where(i => i != frameIndex).ToArray();

            frame.IsLinked = false;
            // Clearing the flag first is what lets the shared copy-on-write helper do the copy for us.
            EnsureFrameHasUniqueSprite(frameIndex);

            if (siblings.Length == 1)
            {
                var last = GetFrameByIndex(siblings[0]);
                if (last != null)
                    last.IsLinked = false;
            }

            return true;
        }

        /// <summary>
        /// Guarantees the frame owns a sprite node (allocating an empty one if the frame was empty), without
        /// the "unshare it" part of <see cref="EnsureFrameHasUniqueSprite"/>.
        /// </summary>
        private void EnsureFrameHasOwnSprite(int frameIndex)
        {
            var frame = GetFrameByIndex(frameIndex);
            if (frame == null || frame.NodeId != Guid.Empty)
                return;

            var sprite = new SpriteNode(Size)
            {
                Position = SKPoint.Empty
            };
            sprite.DesignerState.IsLocked = true;
            SetSpriteToFrame(frame, sprite);
        }

        public bool HasFrameUniqueSprite(int frameIndex) => HasFrameUniqueSprite(GetFrameByIndex(frameIndex));
        private bool HasFrameUniqueSprite(LayerFrameMeta? frame)
        {
            if (frame == null || frame.IsEmpty)
                return false;

            for (var i = 0; i < FrameCount; i++)
            {
                var other = GetFrameByIndex(i);
                //skip checking frame
                if (other == null || other == frame) continue;

                if (frame.NodeId == other.NodeId)
                    return false;
            }

            return true;
        }

        public void MergeFrom(Layer sourceLayer)
        {
            for (var i = 0; i < FrameCount; i++)
            {
                if (!HasFrameUniqueSprite(i))
                    EnsureFrameHasUniqueSprite(i);

                var srcNode = sourceLayer.GetSpriteByFrame(i);
                if (srcNode == null)
                    continue;

                var destNode = GetSpriteByFrame(i);
                destNode?.MergeFrom(srcNode, sourceLayer.Opacity);
            }
        }

        public void RenderFrame(int frameIndex, SKCanvas canvas, ViewPort vp, float opacity = 1f, bool renderHidden = false)
        {
            if (!renderHidden && HiddenFrames.Contains(frameIndex))
                return;

            if (FrameCount > frameIndex)
            {
                //to show layer effects on previews we need apply them before render frames
                //SKNodeRenderer.RenderEffects(canvas, vp, (c, v) =>
                //{
                var layerId = -1;

                if (opacity < 1f || BlendMode != SKBlendMode.SrcOver)
                {
                    var layerPaint = new SKPaint() { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
                    layerPaint.BlendMode = BlendMode;
                    layerId = canvas.SaveLayer(layerPaint);
                }

                var node = GetSpriteByFrame(frameIndex);
                if(node != null)
                    SKNodeRenderer.Render(node, new RenderContext(canvas, vp));

                if (layerId != 1)
                {
                    canvas.Restore();
                }
                //});
            }
        }

        public void RenderCurrentFramePreview(SKBitmap previewBitmap, int i) => RenderPreview(CurrentFrameIndex, previewBitmap, 1);

        private void RenderPreview(int frameIndex, SKBitmap targetBitmap, float scale)
        {
            var node = GetSpriteByFrame(frameIndex);
            if (node == null)
                return;

            var vp = new ViewPort((int)(targetBitmap.Width), (int)(targetBitmap.Height));
            vp.Settings.RenderAdorners = false;

            // SKNodeRenderer.Render applies only the node's LOCAL transform (ancestors are skipped), so the
            // frame is always painted in its own local space (0,0..Size) regardless of where the owning
            // artboard sits in the scene. Fitting the viewport to the layer's GLOBAL bounding box would shift
            // the preview by the artboard's scene offset — making thumbnails of off-origin artboards render
            // displaced. Use the node's local bounds instead so the preview matches what gets drawn.
            var bbox = node.LocalBounds;
            bbox.Inflate(-1, -1);

            vp.ShowArea(bbox);

            using var canvas = targetBitmap.CreateCanvas();
            canvas.Clear(SKColor.Empty);

            SKNodeRenderer.Render(node, new RenderContext(canvas, vp));
            //RenderFrame(frameIndex, canvas, vp, renderHidden: true);
            canvas.Flush();
        }

        public int AddEmptyFrame()
        {
            var frame = InsertFrameMetadata(-1);
            return Frames.IndexOf(frame);
        }

        public int InsertEmptyFrame(int index)
        {
            var frame = InsertFrameMetadata(index);
            return Frames.IndexOf(frame);
        }

        public int DuplicateFrame(int index)
        {
            var frame = InsertFrameMetadata(index + 1, GetSpriteByFrame(index));
            var newIndex = Frames.IndexOf(frame);

            // Duplicating means "an independent copy". The insert above hands the new frame the source's
            // node, which is normally harmless (copy-on-write splits it on the first edit) — but if the
            // SOURCE is a linked cel, copy-on-write is disabled for that node, so the duplicate would
            // silently become a third member of the link and every edit would change all three. Give it its
            // own pixels right away instead, which also keeps the "all frames on a node are linked, or none
            // are" invariant intact.
            if (GetFrameByIndex(index)?.IsLinked == true)
            {
                frame.IsLinked = false;
                EnsureFrameHasUniqueSprite(newIndex);
            }

            return newIndex;
        }

        public int InsertFrameFromBitmap(int index, SKBitmap bitmap)
        {
            var i = InsertEmptyFrame(index);
            EnsureFrameHasUniqueSprite(i);
            if (bitmap.Info.Size != Size)
            {
                throw new InvalidOperationException($"Source bitmap size {bitmap.Info.Size} is not equal to the target bitmap size {Size}");
            }

            SetData(i, bitmap.GetPixelSpan().ToArray());
            return i;
        }
        public void InsertFrameFromBitmapNode(int index, BitmapNode sprite)
        {
            InsertFrameMetadata(index, sprite as SpriteNode);
        }

        private LayerFrameMeta InsertFrameMetadata(int index, SpriteNode? sprite = null)
        {
            var frame = new LayerFrameMeta();
            if (index == -1 || index >= FrameCount)
                Frames.Add(frame);
            else
                Frames.Insert(index, frame);

            SetSpriteToFrame(frame, sprite);
            return frame;
        }

        public void InsertFrameFromNodeId(int index, Guid nodeId)
        {
            if (nodeId == default)
            {
                InsertEmptyFrame(index);
            }
            else
            {
                var nodeIndex = Nodes.FindIndex(x => x.Id == nodeId);
                if (nodeIndex >= 0)
                {
                    var frame = new LayerFrameMeta() { NodeId = nodeId, NodeIndex = nodeIndex };
                    Frames.Insert(index, frame);
                }
                else
                {
                    InsertEmptyFrame(index);
                }
            }
        }

        /// <summary>
        /// Re-inserts a frame from a metadata snapshot taken before it was deleted, restoring everything the
        /// meta carries — including <see cref="LayerFrameMeta.IsLinked"/>.
        /// <para>
        /// Undo cannot go through <see cref="InsertFrameFromNodeId"/>, which rebuilds the meta from a bare
        /// node id and therefore always produces <c>IsLinked = false</c>. Undoing the delete of a linked
        /// frame that way brought it back sharing the group's pixels but *not* flagged linked — exactly the
        /// mixed state the invariant forbids: drawing on a still-linked sibling would silently change this
        /// frame while its tile showed no link, and drawing on this frame would copy-on-write it out of the
        /// link the user thought had been restored.
        /// </para>
        /// <paramref name="sprite"/> is the node captured at delete time when this frame owned it outright;
        /// pass null when the frame shared someone else's node, which is expected to still be attached.
        /// </summary>
        public void InsertFrameFromMeta(int index, LayerFrameMeta meta, SpriteNode? sprite = null)
        {
            if (sprite != null && !Nodes.Contains(sprite))
                Nodes.Add(sprite);

            var restored = LayerFrameMeta.Copy(meta);
            restored.NodeIndex = -1;

            if (sprite != null)
                restored.NodeId = sprite.Id;

            // The node a shared frame pointed at can be gone (its last owner deleted meanwhile); an id no
            // node answers to would make the frame render nothing and never heal, so degrade to empty.
            if (restored.NodeId != Guid.Empty && Nodes.OfType<SpriteNode>().All(x => x.Id != restored.NodeId))
            {
                restored.NodeId = Guid.Empty;
                restored.IsLinked = false;
            }

            if (index < 0 || index >= FrameCount)
                Frames.Add(restored);
            else
                Frames.Insert(index, restored);

            // Restoring a second member revives a group that DeleteFrame collapsed down to one.
            if (restored.IsLinked && restored.NodeId != Guid.Empty)
            {
                foreach (var other in Frames)
                {
                    if (!ReferenceEquals(other, restored) && other.NodeId == restored.NodeId)
                        other.IsLinked = true;
                }
            }
        }

        private void SetSpriteToFrame(LayerFrameMeta frame, SpriteNode? sprite)
        {
            var previousNodeId = frame.NodeId;
            var wasLinked = frame.IsLinked;

            if (frame.NodeId != Guid.Empty && sprite == null)
            {
                var oldSprite = GetSpriteByFrame(frame);
                if (HasFrameUniqueSprite(frame))
                    oldSprite?.RemoveFromParent();
            }

            if (sprite != null && !Nodes.Contains(sprite))
            {
                Nodes.Add(sprite);
            }

            frame.IsKeyFrame = false;
            frame.NodeIndex = -1;
            frame.NodeId = sprite?.Id ?? default;

            // A frame pointed at a different node (or at none) is no longer a member of its old link group.
            // Leaving the flag set is what produces the "linked but sharing nothing" state that blocks
            // copy-on-write forever, so the flag follows the node.
            if (wasLinked && frame.NodeId != previousNodeId)
            {
                frame.IsLinked = false;
                CollapseLinkGroupIfSingle(previousNodeId);
            }
        }

        /// <summary>
        /// Clears <see cref="LayerFrameMeta.IsLinked"/> when only one frame is left pointing at
        /// <paramref name="nodeId"/>. A "linked" cel with no siblings shares nothing: it would keep drawing
        /// the link marker and keep blocking copy-on-write for no reason. Mirrors what
        /// <see cref="UnlinkFrame"/> already does for the frame it breaks out.
        /// </summary>
        private void CollapseLinkGroupIfSingle(Guid nodeId)
        {
            if (nodeId == Guid.Empty)
                return;

            LayerFrameMeta? survivor = null;
            var count = 0;
            foreach (var other in Frames)
            {
                if (!other.IsLinked || other.NodeId != nodeId)
                    continue;

                survivor = other;
                if (++count > 1)
                    return;
            }

            if (count == 1 && survivor != null)
                survivor.IsLinked = false;
        }

        public void DeleteFrame(int index, Action<SpriteNode>? onSpriteDeletedAction = default, Action<Guid>? onEmptyFrameDeletedAction = default)
        {
            var frame = GetFrameByIndex(index);
            if (frame == null)
                return;

            if (HasFrameUniqueSprite(frame))
            {
                var sprite = GetSpriteByFrame(frame);
                if (sprite != null)
                {
                    onSpriteDeletedAction?.Invoke(sprite);
                    sprite.RemoveFromParent();
                }
            }
            else
            {
                onEmptyFrameDeletedAction?.Invoke(frame.NodeId);
            }

            Frames.Remove(frame);

            // Deleting one member of a two-frame link leaves a single frame still flagged linked — the marker
            // would keep claiming a sharing that no longer exists.
            if (frame.IsLinked)
                CollapseLinkGroupIfSingle(frame.NodeId);
        }

        internal void SetFrame(int value)
        {
            CurrentFrameIndex = value;
            EnsureValidFrameIndex();
        }

        private void EnsureValidFrameIndex()
        {
            if (FrameCount == 0) return;

            // Deleting frames can leave CurrentFrameIndex past the end. Re-clamping unconditionally (rather
            // than only when the requested value changed) matters because the sprite may repeat the same
            // index after frames were removed under it — the old early-return kept the stale index alive.
            if (CurrentFrameIndex >= FrameCount)
                CurrentFrameIndex = FrameCount - 1;
            else if (CurrentFrameIndex < 0)
                CurrentFrameIndex = 0;
        }

        public void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
        {
            Size = newSize;
            foreach (var bmNode in Nodes.OfType<BitmapNode>())
                bmNode.Resize(newSize, horizontalAnchor, verticalAnchor);
        }
        public void ResizeImage(SKSize newSize)
        {
            Size = newSize;
            foreach (var bmNode in Nodes.OfType<BitmapNode>())
                bmNode.Resize(newSize);
        }

        public void Crop(SKRect targetBounds)
        {
            Size = targetBounds.Size;
            foreach (var bmNode in Nodes.OfType<BitmapNode>())
                bmNode.Crop(targetBounds);
        }

        public void ClearFrame(int frameIndex)
        {
            var frame = GetFrameByIndex(frameIndex);
            if (frame == null)
                return;

            // A linked cel shares its pixels on purpose, so clearing writes THROUGH the shared node exactly
            // as drawing does: every linked frame clears together and the link survives.
            //
            // Detaching the node instead (the unlinked path below) used to brick the frame: the meta kept
            // IsLinked = true while NodeId became Guid.Empty, and EnsureFrameHasUniqueSprite early-returns on
            // IsLinked, so no sprite was ever rebuilt. Later strokes silently no-opped on a null bitmap, and
            // undo — which restores pixels through SetData -> GetSpriteByFrame — found no sprite and cleared
            // again instead of restoring, so the clear could not even be undone. Only Unlink recovered it.
            if (frame.IsLinked && frame.NodeId != Guid.Empty)
            {
                var shared = GetSpriteByFrame(frame);
                if (shared != null)
                {
                    // Empty data means "erase in place" (BitmapNode.SetData), which keeps the node attached
                    // and invalidates it — the property that makes the clear undoable.
                    shared.SetData([]);
                    return;
                }
            }

            SetSpriteToFrame(frame, null);
        }

        public void RotateSourceBitmap(int frame, bool resize)
        {
            EnsureFrameHasUniqueSprite(frame);
            GetSpriteByFrame(frame)?.RotateSourceBitmap(resize);
        }

        public void FlipSourceBitmap(int frame, FlipMode mode)
        {
            EnsureFrameHasUniqueSprite(frame);
            var sprite = GetSpriteByFrame(frame);
            if (sprite == null)
                return;

            switch (mode)
            {
                case FlipMode.Horizontal:
                    sprite.FlipHorizontal();
                    break;
                case FlipMode.Vertical:
                    sprite.FlipVertical();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public void HideFrame(int frameIndex)
        {
            HiddenFrames.Add(frameIndex);
        }

        public void ShowFrame(int frameIndex)
        {
            HiddenFrames.Remove(frameIndex);
        }

        private HashSet<int> HiddenFrames = new HashSet<int>();

    }
}