using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes
{
    [Obsolete]
    public class AnimatedSprite : SKNode, IUpdatableNode
    {
        private float _timeToNextFrame = 0;
        protected bool _dontInvalidateOnAddChildren;

        [JsonIgnore]
        public override bool DoNotRenderChildren => true;

        [JsonIgnore] public SpriteNode ActiveFrame => Nodes[CurrentFrameIndex] as SpriteNode;

        [JsonIgnore]
        public bool IsPlaying { get; set; } = true;

        public float Rate { get; set; } = 20;
        public int CurrentFrameIndex { get; set; }

        public AnimatedSprite()
        {
        }

        public AnimatedSprite(SKSize size, int framesCount)
        {
//            Bitmap = new SKBitmap(new SKImageInfo((int) size.Width, (int) size.Height));
            Size = size;
            InitFrames(framesCount);
        }

        private void InitFrames(int framesCount)
        {
            for (int i = 0; i < framesCount; i++)
            {
                var frame = SpriteNode.CreateEmpty(Size);
                Nodes.Add(frame);
            }
        }

        public SKBitmap GetDrawingBitmap()
        {
            return ActiveFrame.GetDrawingBitmap();
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            //using (var paint = new SKPaint())
            //{
            //    var i = Index;
            //    paint.Color = SKColors.Purple;
            //    paint.TextSize = 10;
            //    canvas.DrawText($"[{this.GetType().Name} {i}]", 0, 20 + i * 20, paint);
            //}

            ActiveFrame?.OnDraw(canvas, vp);
        }

        protected override void OnChildrenAdded(IEnumerable<SKNode> newNodes)
        {
            base.OnChildrenAdded(newNodes);
            foreach (var newNode in newNodes)
            {
                newNode.IsVisible = false;
            }

            UpdateActiveFrame();

            InvalidateBoundingBoxFromContent();
        }

        internal static AnimatedSprite FromSprites(SpriteNode[] sprites)
        {
            var anim = new AnimatedSprite();

            var bounds = sprites.GetBounds();

            anim._dontInvalidateOnAddChildren = true;
            foreach (var sprite in sprites.OrderBy(x=>x.Name))
            {
                anim.AddFrame(-1, sprite);
            }
            anim._dontInvalidateOnAddChildren = false;

            anim.InvalidateBoundingBoxFromContent();
            return anim;
        }

        public SpriteNode AddFrame(int index = -1, SpriteNode sprite = null)
        {
            if (sprite == null)
            {
                sprite = SpriteNode.CreateEmpty(this.Size);
                sprite.DesignerState.IsLocked = true;
            }

            this.Nodes.Insert(index, sprite);

            UpdateFrameName(sprite);
            sprite.Position = SKPoint.Empty;

            return sprite;
        }

        private void UpdateFrameName(SpriteNode frame)
        {
            frame.Name = Parent.Name + "_frame_" + this.Index;
        }

        public SpriteNode DuplicateFrame(int index, int duplicatedFrameIndex = -1)
        {
            var sprite = this.Nodes[index] as SpriteNode;

            if (sprite == null)
            {
                return null;
            }

            var newSprite = SpriteNode.CreateEmpty(sprite.Size);
            newSprite.Bitmap = sprite.Bitmap.Copy();

            this.Nodes.Insert(duplicatedFrameIndex, newSprite);
            
            UpdateFrameName(newSprite);
            
            newSprite.Position = SKPoint.Empty;

            return newSprite;
        }

        public void DeleteFrame(int index)
        {
            if(index < 0 || index >= Nodes.Count)
                return;
            
            this.Nodes.RemoveAt(index);
        }

        protected override void OnChildrenRemoved(IEnumerable<SKNode> removedNodes)
        {
            base.OnChildrenRemoved(removedNodes);
            InvalidateBoundingBoxFromContent();
        }

        public void InvalidateBoundingBoxFromContent()
        {
            if(_dontInvalidateOnAddChildren)
                return;

            var bbox = GetBoundingBoxWithContent();
            this.Size = bbox.Size;
            this.Position = bbox.Location;
            OnNodeInvalidated();
        }

        private void UpdateActiveFrame()
        {
            if (Nodes.Any())
            {
                if (CurrentFrameIndex >= this.Nodes.Count)
                    CurrentFrameIndex = this.Nodes.Count - 1;

//                Bitmap = ActiveFrame?.Bitmap;
            }
        }

        public virtual void OnUpdate(float dt)
        {
            if (!IsPlaying)
                return;

            _timeToNextFrame -= dt;
            if (_timeToNextFrame <= 0)
            {
                _timeToNextFrame = 1 / Rate;
                SetNextFrame();
            }
        }

        private void SetNextFrame()
        {
            CurrentFrameIndex++;
            if (CurrentFrameIndex >= Nodes.Count)
            {
                CurrentFrameIndex = 0;
            }

            UpdateActiveFrame();
        }

        internal void SetFrame(int value)
        {
            if (value != CurrentFrameIndex)
            {
                CurrentFrameIndex = value;
                if (CurrentFrameIndex >= Nodes.Count)
                {
                    CurrentFrameIndex = Nodes.Count - 1;
                }

                UpdateActiveFrame();
            }
        }

//        public override void InvalidateBitmap()
//        {
//            base.InvalidateBitmap();
//        }

public void RenderFrame(int frameIndex, SKCanvas canvas, ViewPort vp, float opacity = 1f)
        {
            if (Nodes.Count > frameIndex)
            {
                //to show layer effects on previews we need apply them before render frames
                RenderEffects(canvas, vp, (c, v) =>
                {
                    var layerId = -1;

                    if (opacity < 1f || BlendMode != SKBlendMode.SrcOver)
                    {
                        var layerPaint = new SKPaint() { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
                        layerPaint.BlendMode = BlendMode;
                        layerId = c.SaveLayer(layerPaint);
                    }

                    Nodes[frameIndex]?.Render(c, v);

                    if (layerId != 1)
                    {
                        c.Restore();
                    }
                });
            }
        }

        public void RotateCurrentFrameBitmap()
        {
            var sprite = Nodes[CurrentFrameIndex] as SpriteNode;
            sprite?.RotateSourceBitmap();
        }

        public void FlipFrameHorizontal()
        {
            var sprite = Nodes[CurrentFrameIndex] as SpriteNode;
            sprite?.FlipHorizontal();
        }

        public void FlipFrameVertical()
        {
            var sprite = Nodes[CurrentFrameIndex] as SpriteNode;
            sprite?.FlipVertical();
        }

        public void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
        {
            Size = newSize;
            foreach (var spriteNode in Nodes.OfType<SpriteNode>())
            {
                spriteNode.Resize(newSize, horizontalAnchor, verticalAnchor);
            }
        }
        public void Crop(SKRect targetBounds)
        {
            Size = targetBounds.Size;
            foreach (var spriteNode in Nodes.OfType<SpriteNode>())
            {
                spriteNode.Crop(targetBounds);
            }
        }
    }
}