using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Pix2d;
using SkiaNodes.Extensions;
using SkiaNodes.TreeObserver;
using SkiaSharp;

namespace SkiaNodes
{
    public partial class SKNode
    {
        private bool _isDirty = true;

        private SKMatrix _globalTransform;
        private SKRect _boundingBox;
        private SKPoint _position;
        private SKPoint _pivotPosition;
        private SKSize _size;
        private float _rotation;
        private SKMatrix _transform;
        private SKMatrix? _projectionTransform;

        private SKBitmap _renderCache;
        private SKSurface _cacheSurface;
        private ViewPort _cacheViewPort;
        
        private Guid _id;


        public event EventHandler NodeInvalidated;
        public event EventHandler SizeChanged;

        public string Name { get; set; }

        public Guid Id
        {
            get => _id == default ? _id = Guid.NewGuid() : _id ;
            set => _id = value;
        }


        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SKPoint Position
        {
            get => _position;
            set
            {
                _position = value;
                SetDirty();
            }
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SKPoint PivotPosition
        {
            get => _pivotPosition;
            set
            {
                _pivotPosition = value;
                SetDirty();
            }
        }

        public SKSize Size
        {
            get => _size;
            set
            {
                if (Math.Abs(_size.Width - value.Width) > 0.0001 || Math.Abs(_size.Height - value.Height) > 0.0001)
                {
                    _size = value;
                    OnSizeChanged();
                    SetDirty();
                }
            }
        }

        public float Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                SetDirty();
            }
        }

        //[IgnoreDataMember]
        //[JsonIgnore]
        //public SKRect? ClippingRect { get; set; }

        public float Opacity { get; set; } = 1;

        public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

        public List<ISKNodeEffect> Effects { get; set; }

        public bool HasEffects => Effects != null && Effects.Any();

        public bool ShowEffects { get; set; } = true;

        /// <summary>
        /// Rectangle than has zero location and size of current Node
        /// </summary>
        public SKRect LocalBounds => new SKRect(0, 0, Size.Width, Size.Height);

        //[IgnoreDataMember]
        [JsonIgnore]
        public SKMatrix Transform
        {
            get
            {
                if (_isDirty)
                    InvalidateNode();
                return _transform;
            }
        }

        //[IgnoreDataMember]
        [JsonIgnore]
        public SKMatrix? ProjectionTransform
        {
            get
            {
                if (_isDirty)
                    InvalidateNode();
                return _projectionTransform;
            }
            set
            {
                if (!_projectionTransform.Equals(value))
                {
                    _projectionTransform = value;
                    SetDirty();
                }
            }
        }

        //[IgnoreDataMember]
        [JsonIgnore]
        public SKNode Parent { get; private set; }

        //[IgnoreDataMember]
        [JsonIgnore]
        public AdornerLayer AdornerLayer { get; set; }

        //[IgnoreDataMember]
        [JsonIgnore]
        public bool HasAdornerLayer => AdornerLayer != null;

        public NodeCollection Nodes
        {
            get => _nodes ?? (_nodes = new NodeCollection(this));
            set
            {
                if (_nodes == null)
                    _nodes = new NodeCollection(this);

                _nodes.Clear();
                _nodes.AddRange(value);
            }
        }


        //[IgnoreDataMember]
        [JsonIgnore]
        public virtual bool DoNotRenderChildren => false;

        public bool IsInteractive { get; set; }

        public bool IsVisible { get; set; } = true;

        public NodeDesignerState DesignerState { get; set; } = new NodeDesignerState();

        public AttachedComponentsCollection AttachedComponents { get; set; }

        public virtual bool IsAdorner => this is AdornerLayer || CheckIsOnAdornerLayer();
        public int Index => Parent?.Nodes.IndexOf(this) ?? -1;
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? GetType().Name : Name;


        public void Render(SKCanvas canvas, ViewPort vp)
        {
            canvas.SetMatrix(vp.ResultTransformMatrix);

            RenderRecursive(canvas, vp);
        }

        public virtual void RenderRecursive(SKCanvas canvas, ViewPort vp)
        {
            canvas.Save();

            var layerId = -1;

            var clippedAlready = false;

            if (Opacity < 1 || BlendMode != SKBlendMode.SrcOver || HasEffects)
            {
                var layerPaint = new SKPaint() { Color = SKColors.White.WithAlpha((byte)(Opacity * 255)) };

                layerPaint.BlendMode = BlendMode;

                if (this.Parent is IClippingSource cs && cs.ClipMode == SKNodeClipMode.Rect)
                {
                    layerId = canvas.SaveLayer(cs.ClipBounds, layerPaint);
                    clippedAlready = true;
                }
                else
                {
                    layerId = canvas.SaveLayer(layerPaint);
                }
            }

            if (!clippedAlready && Parent is IClippingSource csa && csa.ClipMode == SKNodeClipMode.Rect)
            {
                canvas.ClipRect(csa.ClipBounds);
            }

            //save matrix for adorners before apply transformation to nested nodes
            var adornerMatrix = canvas.TotalMatrix;

            var mt = default(SKMatrix);
            SKMatrix.Concat(ref mt, canvas.TotalMatrix, Transform);
            canvas.SetMatrix(mt);

            RenderEffects(canvas, vp, (c, v) =>
            {
                OnDraw(c, vp);

                if (DoNotRenderChildren) return;

                for (int i = 0; i < Nodes.Count; i++)
                {
                    var node = Nodes[i];
                    if (node == null || !node.IsVisible)
                        continue;

                    node.RenderRecursive(c, v);
                    node.RenderAdorner(c, v, adornerMatrix);
                }
            });

            if (layerId > -1)
                canvas.Restore();

            canvas.Restore();
        }

        protected virtual void RenderAdorner(SKCanvas canvas, ViewPort vp, SKMatrix adornerTransform)
        {
            if (!HasAdornerLayer || !vp.Settings.RenderAdorners) return;

            if (!AdornerLayer.Nodes.Any())
                return;

            canvas.Save();
            canvas.SetMatrix(adornerTransform);
            AdornerLayer.RenderRecursive(canvas, vp);
            canvas.Restore();
        }

        protected void RenderEffects(SKCanvas canvas, ViewPort vp, Action<SKCanvas, ViewPort> renderBranch)
        {
            var hasEffects = Effects != null && Effects.Any();

            if (!ShowEffects || !hasEffects)
            {
                renderBranch(canvas, vp);
                return;
            }

            var cache = GetRenderCacheBitmap();
            cache.Erase(SKColor.Empty);
            var cacheCanvas = _cacheSurface.Canvas;
            renderBranch(cacheCanvas, _cacheViewPort);

            if (hasEffects)
            {
                //var backEffects = new Queue<ISKNodeEffect>(Effects.Where(x => x.Name == "Shadow"));
                var backEffects = new Queue<ISKNodeEffect>(Effects.Where(x => x.EffectType == EffectType.BackEffect));
                if (backEffects.Any())
                {
                    var effect = backEffects.Dequeue();
                    effect.Render(canvas, vp, this, cache, backEffects);
                }

                //var replaceEffects = new Queue<ISKNodeEffect>(Effects.Where(x => x.Name == "Blur" || x.Name == "Grayscale"));
                var replaceEffects = new Queue<ISKNodeEffect>(Effects.Where(x => x.EffectType == EffectType.ReplaceEffect));
                if (!replaceEffects.Any())
                {
                    var rect = new SKRect(0, 0, vp.Size.Width, vp.Size.Height);
                    canvas.DrawBitmap(cache, rect);
                }
                else
                {
                    var effect = replaceEffects.Dequeue();
                    effect.Render(canvas, vp, this, cache, replaceEffects);
                }

                //var overlayEffects = new Queue<ISKNodeEffect>(Effects.Where(x => x.Name == "Color overlay"));
                var overlayEffects = new Queue<ISKNodeEffect>(Effects.Where(x => x.EffectType == EffectType.OverlayEffect));
                if (overlayEffects.Any())
                {
                    var effect = overlayEffects.Dequeue();
                    effect.Render(canvas, vp, this, cache, overlayEffects);
                }
            }
            else
            {
                canvas.DrawBitmap(cache, 0, 0);
            }

        }

        public SKMatrix GetGlobalTransform()
        {
            if (_isDirty)
                InvalidateNode();

            return _globalTransform;
        }

        public SKRect GetBoundingBox()
        {
            if (_isDirty)
                InvalidateNode();

            return _boundingBox;
        }

        public SKRect GetBoundingBoxWithContent()
        {
            var bbox = GetBoundingBox();
            foreach (var node in Nodes)
            {
                bbox.Union(node.GetBoundingBoxWithContent());
            }
            return bbox;
        }

        public void SetDirty()
        {
            _isDirty = true;
            foreach (var node in Nodes)
                node.SetDirty();
        }

        private void InvalidateNode()
        {
            //calculate local transform from Position and Rotation
            var rot = SKMatrix.CreateRotationDegrees(Rotation, PivotPosition.X, PivotPosition.Y);
            var localTransform = SKMatrix.CreateTranslation(Position.X - PivotPosition.X, Position.Y - PivotPosition.Y);
            SKMatrix.Concat(ref localTransform, localTransform, rot);
            _transform = localTransform;

            //recalculate global transform
            _globalTransform = _transform;
            if (Parent != null)
                SKMatrix.Concat(ref _globalTransform, Parent.GetGlobalTransform(), _transform);

            // invalidate bounding box
            ComputeBoundingBox(Size, PivotPosition, _projectionTransform);

            _isDirty = false;
            OnNodeInvalidated();
        }

        protected virtual void ComputeBoundingBox(SKSize size, SKPoint pivot, SKMatrix? projectionTransform)
        {
            var transform = _globalTransform;

            if (projectionTransform.HasValue)
            {
                SKMatrix.Concat(ref transform, transform, projectionTransform.Value);
            }

            var cornerPoints = transform.MapPoints(new[]
            {
                new SKPoint(0, 0),
                new SKPoint(size.Width, 0),
                new SKPoint(0, size.Height),
                new SKPoint(size.Width, size.Height)
            });

            _boundingBox = new SKRect(
                cornerPoints.Min(p => p.X),
                cornerPoints.Min(p => p.Y),
                cornerPoints.Max(p => p.X),
                cornerPoints.Max(p => p.Y));
        }

        /// <summary>
        /// Check if node's bounding box contains point
        /// Used for check if pointer hits node (selection and interactive objects)
        /// </summary>
        /// <param name="worldPos">Point in world coordinates</param>
        /// <returns></returns>
        public virtual bool ContainsPoint(SKPoint worldPos)
        {
            return GetHitZone().Contains(worldPos);
        }

        public virtual SKRect GetHitZone()
        {
            if (Parent is IClippingSource cs)
            {
                if (cs.ClipMode == SKNodeClipMode.Rect)
                {
                    var clipBounds = Parent.GetGlobalTransform().MapRect(cs.ClipBounds);
                    var clippedBounds = SKRect.Intersect(GetBoundingBox(), clipBounds);
                    return clippedBounds;
                }
            }

            return GetBoundingBox();
        }

        public virtual void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            DrawHitZone(canvas, vp, 2, SKColors.Red);

            DrawBoundingBox(canvas, vp, 2, SKColors.Blue);
        }

        public virtual void DrawBoundingBox(SKCanvas canvas, ViewPort vp, float thikness, SKColor color)
        {
            DrawRect(GetBoundingBox(), canvas, vp, thikness, color);
            //#if DEBUG
            //            using (var paint = new SKPaint())
            //            {
            //                paint.Color = color;
            //                paint.TextSize = 10;
            //                canvas.DrawText($"{this.Name}[{this.GetType().Name}]", 30* GetNestingLevel(), 20+ 20 * Index, paint);
            //            }
            //#endif
        }

        public virtual void DrawHitZone(SKCanvas canvas, ViewPort vp, float thikness, SKColor color) => DrawRect(GetHitZone(), canvas, vp, thikness, color);

        protected void DrawRect(SKRect bbox, SKCanvas canvas, ViewPort vp, float thikness, SKColor color)
        {
            canvas.Save();

            var transform = vp.ResultTransformMatrix;
            canvas.SetMatrix(transform);

            using (var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(thikness), color))
            {
                canvas.DrawRect(bbox.Left, bbox.Top, bbox.Width, bbox.Height, paint);
            }

            canvas.Restore();
        }

        protected virtual void OnChildrenAdded(IEnumerable<SKNode> newNodes)
        {
            var nodesToNotify = newNodes.Where(x => !x.IsAdorner).ToArray();
            if (nodesToNotify.Length > 0)
                SceneTreeObserver.OnNodesAdded(this, nodesToNotify);
        }

        protected virtual void OnChildrenRemoved(IEnumerable<SKNode> removedNodes)
        {
            var nodesToNotify = removedNodes.Where(x => !x.IsAdorner).ToArray();
            if (nodesToNotify.Length > 0)
                SceneTreeObserver.OnNodesRemoved(this, nodesToNotify);
        }

        public void Show()
        {
            IsVisible = true;
        }
        public virtual void Hide()
        {
            IsVisible = false;
        }

        protected void OnNodeInvalidated()
        {
            NodeInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Traverse through node tree and return all visible descendands of current node
        /// </summary>
        /// <param name="filterCondition">return only nodes if condition returns True</param>
        /// <param name="includeAdorners">add adorner layer and it's nodes to result</param>
        /// <param name="includeSelf">include this node in result enumeration</param>
        /// <returns></returns>
        public IEnumerable<SKNode> GetVisibleDescendants(Func<SKNode, bool> filterCondition = null, bool includeAdorners = true, bool includeSelf = false)
        {
            var toProcess = new Stack<SKNode>();

            void PushNode(SKNode node)
            {
                if (includeAdorners && node.HasAdornerLayer)
                    toProcess.Push(node.AdornerLayer);

                toProcess.Push(node);
            }

            PushNode(this);

            while (toProcess.Any())
            {
                var curNode = toProcess.Pop();

                if (!curNode.IsVisible)
                    continue;

                if ((includeSelf || this != curNode) && (filterCondition == null || filterCondition(curNode)))
                    yield return curNode;

                for (var i = curNode.Nodes.Count - 1; i >= 0; i--)
                {
                    var curChild = curNode.Nodes[i];
                    if (curChild == null)
                    {
                        // This means that the node structure was changed in race condition and we probably want to skip
                        // this frame. Otherwise something terrible might happen.
                        #if DEBUG
                        Debugger.Break();
                        #else
                        yield break;
                        #endif
                    }

                    PushNode(curChild);
                }
            }
        }

        /// <summary>
        /// Traverse through node tree and return all visible descendands of current node
        /// </summary>
        /// <param name="filterCondition">return only nodes if condition returns True</param>
        /// <param name="includeAdorners">add adorner layer and it's nodes to result</param>
        /// <param name="includeSelf">include this node in result enumeration</param>
        /// <returns></returns>
        public IEnumerable<SKNode> GetDescendants(Func<SKNode, bool> filterCondition = null, bool includeAdorners = true, bool includeSelf = false)
        {
            var toProcess = new Stack<SKNode>();

            void PushNode(SKNode node)
            {
                if (includeAdorners && node.HasAdornerLayer)
                    toProcess.Push(node.AdornerLayer);

                toProcess.Push(node);
            }

            PushNode(this);

            while (toProcess.Any())
            {
                var curNode = toProcess.Pop();

                if ((includeSelf || this != curNode) && (filterCondition == null || filterCondition(curNode)))
                    yield return curNode;

                for (var i = curNode.Nodes.Count - 1; i >= 0; i--)
                {
                    var curChild = curNode.Nodes[i];

                    PushNode(curChild);
                }
            }
        }


        protected virtual void OnSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        public SKPoint GetLocalPosition(SKPoint worldPosition)
        {
            if (GetGlobalTransform().TryInvert(out var invertedWorldTransform))
            {
                return invertedWorldTransform.MapPoint(worldPosition);
            }

            throw new InvalidOperationException("Can't get local position");
        }
        public SKPoint GetGlobalPosition()
        {
            return GetGlobalTransform().MapPoint(PivotPosition.X, PivotPosition.Y);
        }

        public int GetNestingLevel()
        {
            var n = Parent;
            var i = 0;
            while (n != null)
            {
                i++;
                n = n.Parent;
            }

            return i;
        }

        public bool IsDescendantOf(SKNode other)
        {
            var n = Parent;
            while (n != null)
            {
                if (n == other)
                    return true;

                n = n.Parent;
            }

            return false;
        }

        public bool CheckIsOnAdornerLayer()
        {
            var n = Parent;
            while (n != null)
            {
                if (n is AdornerLayer)
                    return true;

                n = n.Parent;
            }
            return false;
        }

        public void SetGlobalPosition(SKPoint newGlobalPosition)
        {
            var newLocalPosition = Parent?.GetLocalPosition(newGlobalPosition) ?? newGlobalPosition;
            this.Position = newLocalPosition;
        }

        public void CreateRandomName()
        {
            this.Name = RandomString(8);
        }

        private static Random random = new Random();

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public SKBitmap GetRenderCacheBitmap()
        {
            if (_renderCache == null)
            {
                _renderCache = new SKBitmap(new SKImageInfo((int)Size.Width, (int)Size.Height, Pix2DAppSettings.ColorType));

                _cacheViewPort = new ViewPort(_renderCache.Width, _renderCache.Height);
            }

            if (_cacheSurface == null)
            {
                _cacheSurface = SKSurface.Create(_renderCache.Info, _renderCache.GetPixels(), _renderCache.Width * 4);
            }

            return _renderCache;
        }

        public void Unload()
        {
            OnUnload();
            DeleteCacheBitmap();
            for (var i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i];
                node.Unload();
            }
        }

        private void DeleteCacheBitmap()
        {
            _cacheSurface?.Dispose();
            _cacheSurface = null;

            _renderCache?.Dispose();
            _renderCache = null;
        }

        /// <summary>
        /// Use it to release resources like SKBitmap
        /// </summary>
        public virtual void OnUnload()
        {

        }
    }
}
