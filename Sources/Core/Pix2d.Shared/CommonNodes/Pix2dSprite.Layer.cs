#nullable enable
using Newtonsoft.Json;
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

    [JsonIgnore] public bool IsEmpty => NodeIndex == -1 && NodeId == Guid.Empty;

    public override string ToString()
    {
        return $"{NodeIndex} : {NodeId} : kf";
    }

    public static LayerFrameMeta Copy(LayerFrameMeta other)
    {
        return new LayerFrameMeta()
        {
            IsKeyFrame = other.IsKeyFrame,
            NodeIndex = other.NodeIndex,
            NodeId = other.NodeId
        };
    }
}

public partial class Pix2dSprite
{
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
            var copy = this.Clone();
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (copy.Nodes[i] is SpriteNode sprite)
                {
                    sprite.Bitmap = sprite.Bitmap.Copy();
                }
            }

            return copy;
        }

        private SpriteNode? GetActiveFrameSprite() => GetSpriteByFrame(GetFrameByIndex(CurrentFrameIndex));
        public SpriteNode? GetSpriteByFrame(int index) => GetSpriteByFrame(GetFrameByIndex(index));

        private LayerFrameMeta GetFrameByIndex(int index)
        {
            EnsureFramesInitialized();
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
                for (var i = 0; i < Nodes.Count; i++) InsertFrameFromBitmapNode(i, (BitmapNode)Nodes[i]);
            }
        }

        private SpriteNode? GetSpriteByFrame(LayerFrameMeta frame)
        {
            return frame.NodeId == Guid.Empty
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
            if (Parent is Pix2dSprite parentSprite && parentSprite.OnionSkinSettings.IsEnabled)
            {
                var prevIndex = CurrentFrameIndex - 1;
                if (prevIndex < 0)
                    prevIndex = FrameCount - 1;
                if (GetSpriteByFrame(prevIndex) is SKNode prevFrame)
                    SKNodeRenderer.Render(prevFrame, new RenderContext(canvas, vp, 0.3f));
            }

            if (HiddenFrames.Contains(CurrentFrameIndex)
                || GetActiveFrameSprite() is not SKNode node)
                return;
            SKNodeRenderer.Render(node, new RenderContext(canvas, vp));
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

            if (data == null)
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
                if (HasFrameUniqueSprite(frame))
                    return;

                var sprite = new SpriteNode(this.Size);

                if (!frame.IsEmpty) //copy data from old sprite if the the frame wasn't empty
                {
                    var srcSprite = GetSpriteByFrame(frame);
                    var srcData = srcSprite?.GetData();
                    if (srcData != null)
                        sprite.SetData(srcData);
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

        public bool HasFrameUniqueSprite(int frameIndex) => HasFrameUniqueSprite(GetFrameByIndex(frameIndex));
        private bool HasFrameUniqueSprite(LayerFrameMeta frame)
        {
            if (frame.IsEmpty)
                return false;

            for (var i = 0; i < FrameCount; i++)
            {
                var other = GetFrameByIndex(i);
                //skip checking frame
                if (other == frame) continue;

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
                destNode.MergeFrom(srcNode, sourceLayer.Opacity);
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

            var bbox = GetBoundingBox();
            bbox.Left = 1;
            bbox.Top = 1;
            bbox.Right -= 1;
            bbox.Bottom -= 1;

            vp.ShowArea(bbox);

            using var canvas = targetBitmap.GetSKSurface().Canvas;
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
            return Frames.IndexOf(frame);
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

        private void SetSpriteToFrame(LayerFrameMeta frame, SpriteNode? sprite)
        {
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
        }

        public void DeleteFrame(int index, Action<SpriteNode>? onSpriteDeletedAction = default, Action<Guid>? onEmptyFrameDeletedAction = default)
        {
            var frame = GetFrameByIndex(index);
            if (HasFrameUniqueSprite(frame))
            {
                var sprite = GetSpriteByFrame(frame);
                onSpriteDeletedAction?.Invoke(sprite);
                sprite.RemoveFromParent();
            }
            else
            {
                onEmptyFrameDeletedAction?.Invoke(frame.NodeId);
            }

            Frames.Remove(frame);
        }

        internal void SetFrame(int value)
        {
            if (value == CurrentFrameIndex) return;

            CurrentFrameIndex = value;
            if (CurrentFrameIndex >= FrameCount)
            {
                CurrentFrameIndex = FrameCount - 1;
            }

            EnsureValidFrameIndex();
        }

        private void EnsureValidFrameIndex()
        {
            if (FrameCount == 0) return;

            if (CurrentFrameIndex >= FrameCount)
                CurrentFrameIndex = FrameCount - 1;
        }

        public void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
        {
            Size = newSize;
            foreach (var bmNode in Nodes.OfType<BitmapNode>())
                bmNode.Resize(newSize, horizontalAnchor, verticalAnchor);
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
            SetSpriteToFrame(frame, null);
        }

        public void RotateSourceBitmap(int frame, bool resize)
        {
            EnsureFrameHasUniqueSprite(frame);
            GetSpriteByFrame(frame)?.RotateSourceBitmap(resize);
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