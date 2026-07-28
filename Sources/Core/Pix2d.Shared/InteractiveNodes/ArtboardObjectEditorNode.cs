#nullable enable
using Pix2d.Abstract;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

/// <summary>
/// The interactive overlay of an artboard canvas-edit session (Resize / Crop — see
/// <c>IArtboardObjectEditService</c>): a working frame with 4 corner + 4 edge handles over one artboard.
/// Dragging a handle only edits <see cref="FrameRect"/> — the sprite's pixels and size are untouched until
/// the service applies the result, so a cancel needs no rollback. Presses anywhere else are swallowed
/// (inside the artboard body and across the rest of the viewport) so they never reach a drawing tool or the
/// object-selection tool underneath; the user confirms or cancels from the action bar (or with Esc).
///
/// Moving an artboard is deliberately not handled here — that is a plain drag in the General context
/// (<c>ObjectManipulationTool</c> + the object selection frame).
/// </summary>
public class ArtboardObjectEditorNode : SKNode, IViewPortBindable
{
    private enum Corner { LeftTop, RightTop, RightBottom, LeftBottom }
    private enum Edge { Top, Right, Bottom, Left }

    private const float HandleHitPx = 22f;     // grab area
    private const float HandleVisualPx = 11f;  // drawn square
    private static readonly SKColor Accent = new(0x29, 0xB0, 0xF3);

    private readonly BackdropNode _backdrop;
    private readonly BlockerNode _interiorBlocker;
    private readonly InvisibleThumb[] _corners = new InvisibleThumb[4];
    private readonly InvisibleThumb[] _edges = new InvisibleThumb[4];
    private readonly FrameInfoBadgeNode _infoBadge;

    private Pix2dSprite? _sprite;
    private SKRect _frameRect;
    private SKRect _dragStartFrame;
    private float _handleWorldSize;
    private ViewPort? _vp;

    /// <summary>Called on every live change so the host can refresh the viewport.</summary>
    public Action? OnChanged { get; set; }

    public ArtboardObjectEditMode Mode { get; private set; } = ArtboardObjectEditMode.Resize;

    public SKRect FrameRect => _frameRect;

    public ArtboardObjectEditorNode()
    {
        Name = "Artboard object editor";

        _backdrop = new BackdropNode();
        _interiorBlocker = new BlockerNode { FrameProvider = () => _frameRect };

        Nodes.Add(_backdrop);          // bottom: swallows presses across the rest of the viewport
        Nodes.Add(_interiorBlocker);   // swallows presses inside the artboard body (no draw-through)

        for (var i = 0; i < 4; i++)
        {
            var corner = (Corner)i;
            var thumb = new InvisibleThumb();
            thumb.DragStarted += (_, _) => BeginResizeDrag();
            thumb.DragDelta += (_, e) => OnCornerDrag(corner, new SKPoint(e.HorizontalChange, e.VerticalChange));
            _corners[i] = thumb;
            Nodes.Add(thumb); // top: corner handles win the hit-test over the blocker
        }

        for (var i = 0; i < 4; i++)
        {
            var edge = (Edge)i;
            var thumb = new InvisibleThumb();
            thumb.DragStarted += (_, _) => BeginResizeDrag();
            thumb.DragDelta += (_, e) => OnEdgeDrag(edge, new SKPoint(e.HorizontalChange, e.VerticalChange));
            _edges[i] = thumb;
            Nodes.Add(thumb); // mid-edge handles resize a single dimension
        }

        _infoBadge = new FrameInfoBadgeNode { InfoProvider = GetFrameInfo };
        Nodes.Add(_infoBadge); // non-interactive HUD floating under the frame
    }

    /// <summary>Targets an artboard for the given sub-mode; the working frame starts at its current bounds.</summary>
    public void SetTarget(Pix2dSprite sprite, ArtboardObjectEditMode mode)
    {
        _sprite = sprite;
        Mode = mode;
        _frameRect = sprite.GetBoundingBox();
        Layout();
    }

    public void OnViewChanged(ViewPort vp)
    {
        _vp = vp;
        _handleWorldSize = vp.PixelsToWorld(HandleHitPx);
        Layout();
    }

    private void BeginResizeDrag() => _dragStartFrame = _frameRect;

    private void OnCornerDrag(Corner corner, SKPoint delta)
    {
        var f = _dragStartFrame;
        float left = f.Left, top = f.Top, right = f.Right, bottom = f.Bottom;

        switch (corner)
        {
            case Corner.LeftTop: left = f.Left + delta.X; top = f.Top + delta.Y; break;
            case Corner.RightTop: right = f.Right + delta.X; top = f.Top + delta.Y; break;
            case Corner.RightBottom: right = f.Right + delta.X; bottom = f.Bottom + delta.Y; break;
            case Corner.LeftBottom: left = f.Left + delta.X; bottom = f.Bottom + delta.Y; break;
        }

        left = MathF.Round(left);
        top = MathF.Round(top);
        right = MathF.Round(right);
        bottom = MathF.Round(bottom);

        // Keep the un-dragged edge fixed; never let the dragged edge cross it (min 1px canvas).
        if (right - left < 1)
        {
            if (corner is Corner.LeftTop or Corner.LeftBottom) left = right - 1;
            else right = left + 1;
        }
        if (bottom - top < 1)
        {
            if (corner is Corner.LeftTop or Corner.RightTop) top = bottom - 1;
            else bottom = top + 1;
        }

        _frameRect = new SKRect(left, top, right, bottom);
        Layout();
        OnChanged?.Invoke();
    }

    private void OnEdgeDrag(Edge edge, SKPoint delta)
    {
        var f = _dragStartFrame;
        float left = f.Left, top = f.Top, right = f.Right, bottom = f.Bottom;

        switch (edge)
        {
            case Edge.Left: left = f.Left + delta.X; break;
            case Edge.Right: right = f.Right + delta.X; break;
            case Edge.Top: top = f.Top + delta.Y; break;
            case Edge.Bottom: bottom = f.Bottom + delta.Y; break;
        }

        left = MathF.Round(left);
        top = MathF.Round(top);
        right = MathF.Round(right);
        bottom = MathF.Round(bottom);

        // Keep the un-dragged edge fixed; never let the dragged edge cross it (min 1px canvas).
        if (right - left < 1)
        {
            if (edge == Edge.Left) left = right - 1;
            else if (edge == Edge.Right) right = left + 1;
        }
        if (bottom - top < 1)
        {
            if (edge == Edge.Top) top = bottom - 1;
            else if (edge == Edge.Bottom) bottom = top + 1;
        }

        _frameRect = new SKRect(left, top, right, bottom);
        Layout();
        OnChanged?.Invoke();
    }

    private FrameInfoBadgeNode.FrameInfo? GetFrameInfo()
    {
        if (_frameRect.Width <= 0 || _frameRect.Height <= 0)
            return null;

        // The canvas-edit frame has no rotation, so the rect already is the world-space region.
        return new FrameInfoBadgeNode.FrameInfo(
            _frameRect, new SKPoint(_frameRect.Left, _frameRect.Top), _frameRect.Size, 0);
    }

    private void Layout()
    {
        var hs = _handleWorldSize > 0 ? _handleWorldSize : 22f;

        PlaceCorner(Corner.LeftTop, new SKPoint(_frameRect.Left, _frameRect.Top), hs);
        PlaceCorner(Corner.RightTop, new SKPoint(_frameRect.Right, _frameRect.Top), hs);
        PlaceCorner(Corner.RightBottom, new SKPoint(_frameRect.Right, _frameRect.Bottom), hs);
        PlaceCorner(Corner.LeftBottom, new SKPoint(_frameRect.Left, _frameRect.Bottom), hs);

        PlaceHandle(_edges[(int)Edge.Top], new SKPoint(_frameRect.MidX, _frameRect.Top), hs);
        PlaceHandle(_edges[(int)Edge.Right], new SKPoint(_frameRect.Right, _frameRect.MidY), hs);
        PlaceHandle(_edges[(int)Edge.Bottom], new SKPoint(_frameRect.MidX, _frameRect.Bottom), hs);
        PlaceHandle(_edges[(int)Edge.Left], new SKPoint(_frameRect.Left, _frameRect.MidY), hs);

        if (_vp != null)
        {
            var visible = _vp.GetVisibleArea();
            _backdrop.Position = visible.Location;
            _backdrop.Size = visible.Size;
        }
    }

    private void PlaceCorner(Corner corner, SKPoint p, float hs) => PlaceHandle(_corners[(int)corner], p, hs);

    private static void PlaceHandle(InvisibleThumb thumb, SKPoint p, float hs)
    {
        thumb.Size = new SKSize(hs, hs);
        thumb.Position = new SKPoint(p.X - hs / 2f, p.Y - hs / 2f);
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        _vp = vp;

        var stroke = vp.PixelsToWorld(2);
        var visual = vp.PixelsToWorld(HandleVisualPx);

        canvas.Save();
        canvas.SetMatrix(vp.ResultTransformMatrix);

        using var border = new SKPaint { IsStroke = true, IsAntialias = true, StrokeWidth = stroke, Color = Accent };
        canvas.DrawRect(_frameRect, border);

        using var fill = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var handleStroke = new SKPaint { IsStroke = true, IsAntialias = true, StrokeWidth = stroke, Color = Accent };

        DrawHandle(canvas, new SKPoint(_frameRect.Left, _frameRect.Top), visual, fill, handleStroke);
        DrawHandle(canvas, new SKPoint(_frameRect.Right, _frameRect.Top), visual, fill, handleStroke);
        DrawHandle(canvas, new SKPoint(_frameRect.Right, _frameRect.Bottom), visual, fill, handleStroke);
        DrawHandle(canvas, new SKPoint(_frameRect.Left, _frameRect.Bottom), visual, fill, handleStroke);

        DrawHandle(canvas, new SKPoint(_frameRect.MidX, _frameRect.Top), visual, fill, handleStroke);
        DrawHandle(canvas, new SKPoint(_frameRect.Right, _frameRect.MidY), visual, fill, handleStroke);
        DrawHandle(canvas, new SKPoint(_frameRect.MidX, _frameRect.Bottom), visual, fill, handleStroke);
        DrawHandle(canvas, new SKPoint(_frameRect.Left, _frameRect.MidY), visual, fill, handleStroke);

        canvas.Restore();
    }

    private static void DrawHandle(SKCanvas canvas, SKPoint p, float size, SKPaint fill, SKPaint stroke)
    {
        var r = new SKRect(p.X - size / 2f, p.Y - size / 2f, p.X + size / 2f, p.Y + size / 2f);
        canvas.DrawRect(r, fill);
        canvas.DrawRect(r, stroke);
    }

    /// <summary>Invisible draggable handle — visuals are drawn by the parent under the viewport transform.</summary>
    private sealed class InvisibleThumb : Thumbs.ThumbNode
    {
        protected override void OnDraw(SKCanvas canvas, ViewPort vp) { }
    }

    /// <summary>Invisible catch-all over the artboard body that swallows presses (so they never reach a
    /// drawing tool) without doing anything else. Bounded to the live frame rect.</summary>
    private sealed class BlockerNode : SKNode
    {
        public Func<SKRect>? FrameProvider { get; set; }

        public BlockerNode()
        {
            IsInteractive = true;
            Name = "Artboard editor body blocker";
        }

        public override bool ContainsPoint(SKPoint worldPos)
            => FrameProvider?.Invoke().Contains(worldPos) ?? false;

        public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
        {
            base.OnPointerPressed(eventArgs, clickCount);
            eventArgs.Handled = true;
        }

        protected override void OnDraw(SKCanvas canvas, ViewPort vp) { }
    }

    /// <summary>Full-viewport catch-all behind the frame: a press here is marked handled so it never reaches
    /// a drawing tool or the object-selection tool on the root node. The session is ended only from the
    /// action bar / Esc, so an outside press is simply ignored.</summary>
    private sealed class BackdropNode : SKNode
    {
        public BackdropNode()
        {
            IsInteractive = true;
            Name = "Artboard editor backdrop";
        }

        public override bool ContainsPoint(SKPoint worldPos) => true;

        public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
        {
            base.OnPointerPressed(eventArgs, clickCount);
            eventArgs.Handled = true;
        }

        protected override void OnDraw(SKCanvas canvas, ViewPort vp) { }
    }
}
