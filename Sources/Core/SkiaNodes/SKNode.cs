using Newtonsoft.Json;
using SkiaNodes.Extensions;
using SkiaNodes.TreeObserver;
using SkiaSharp;

namespace SkiaNodes;

public partial class SKNode
{
    protected SKColor? _bboxColor;
    [JsonIgnore]
    protected virtual SKColor BBoxColor => _bboxColor ??= new SKColor((byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256));

    public bool IsDirty { get; private set; } = true;

    private SKMatrix _globalTransform;
    private SKRect _boundingBox;
    private SKPoint _position;
    private SKPoint _pivotPosition;
    private SKSize _size;
    private float _rotation;
    private SKMatrix _transform;
    private SKMatrix? _projectionTransform;

    private Guid _id;


    public event EventHandler NodeInvalidated;
    public event EventHandler SizeChanged;

    public string Name { get; set; } = "New node";

    public Guid Id
    {
        get => _id == Guid.Empty ? _id = Guid.NewGuid() : _id ;
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

    public List<ISKNodeEffect> Effects { get; set; } = [];

    public bool HasEffects => Effects.Any();

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
            if (IsDirty)
                InvalidateNode();
            return _transform;
        }
    }

    [JsonIgnore]
    public SKMatrix? ProjectionTransform
    {
        get
        {
            if (IsDirty)
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

    [JsonIgnore]
    public SKNode? Parent { get; private set; }

    [JsonIgnore]
    public AdornerLayer? AdornerLayer { get; set; }

    [JsonIgnore]
    public bool HasAdornerLayer => AdornerLayer != null;

    public NodeCollection Nodes { get; private set; }

    public bool IsInteractive { get; set; }

    public bool IsVisible { get; set; } = true;

    public NodeDesignerState DesignerState { get; set; } = new();

    public virtual bool IsAdorner => this is AdornerLayer || CheckIsOnAdornerLayer();
    public int Index => Parent?.Nodes.IndexOf(this) ?? -1;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? GetType().Name : Name;

    public SKMatrix GetGlobalTransform()
    {
        if (IsDirty)
            InvalidateNode();

        return _globalTransform;
    }

    public SKRect GetBoundingBox()
    {
        if (IsDirty)
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
        IsDirty = true;
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

        IsDirty = false;
        OnNodeInvalidated();
    }

    protected virtual void ComputeBoundingBox(SKSize size, SKPoint pivot, SKMatrix? projectionTransform)
    {
        var transform = _globalTransform;

        if (projectionTransform.HasValue)
        {
            SKMatrix.Concat(ref transform, transform, projectionTransform.Value);
        }

        var cornerPoints = transform.MapPoints([
            new SKPoint(0, 0),
            new SKPoint(size.Width, 0),
            new SKPoint(0, size.Height),
            new SKPoint(size.Width, size.Height)
        ]);

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

    protected internal virtual void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        this.DrawHitZone(canvas, vp, 2, SKColors.Red);

        this.DrawBoundingBox(canvas, vp, 2, SKColors.BurlyWood);
    }

    protected virtual void DrawDebugStuff(SKCanvas canvas, ViewPort vp)
    {
        DrawHitZone(canvas, vp, 2, SKColors.Red);

        DrawBoundingBox(canvas, vp, 2, BBoxColor);

        using var paint = new SKPaint();
        paint.Color = BBoxColor;
        paint.TextSize = 14;
        canvas.DrawText($"{this.Name}[{this.GetType().Name}]", 10 * GetNestingLevel(), 20 + 20 * Index, paint);
    }

    public virtual void DrawBoundingBox(SKCanvas canvas, ViewPort vp, float thickness, SKColor color)
    {
        DrawRect(GetBoundingBox(), canvas, vp, thickness, color);
    }

    public virtual void DrawHitZone(SKCanvas canvas, ViewPort vp, float thickness, SKColor color) => DrawRect(GetHitZone(), canvas, vp, thickness, color);

    protected void DrawRect(SKRect bbox, SKCanvas canvas, ViewPort vp, float thickness, SKColor color)
    {
        canvas.Save();

        var transform = vp.ResultTransformMatrix;
        canvas.SetMatrix(transform);

        using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(thickness), color);
        canvas.DrawRect(bbox.Left, bbox.Top, bbox.Width, bbox.Height, paint);

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

    protected void OnNodeInvalidated()
    {
        NodeInvalidated?.Invoke(this, EventArgs.Empty);
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

    public void Unload()
    {
        OnUnload();
        foreach (var node in Nodes) 
            node.Unload();
    }

    /// <summary>
    /// Use it to release resources like SKBitmap
    /// </summary>
    public virtual void OnUnload()
    {

    }
}