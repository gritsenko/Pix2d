#nullable enable
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes.Thumbs;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

/// <summary>
/// The interactive frame shown in "edit sprite as object" mode. Lets the user move the artboard anywhere
/// in the scene (drag the body) and resize its canvas via 4 corner handles (crop-tool semantics — no
/// rotation). The node only manipulates a working world rectangle (<see cref="FrameRect"/>) and live-moves
/// the sprite during a body drag for feedback; the actual undoable crop/move is committed by
/// <c>ArtboardObjectEditService</c> when the user clicks outside the frame (see <see cref="ApplyRequested"/>).
/// </summary>
public class ArtboardObjectEditorNode : SKNode, IViewPortBindable
{
    private enum Corner { LeftTop, RightTop, RightBottom, LeftBottom }
    private enum Edge { Top, Right, Bottom, Left }

    private const float HandleHitPx = 22f;     // grab area
    private const float HandleVisualPx = 11f;  // drawn square
    private static readonly SKColor Accent = new(0x29, 0xB0, 0xF3);

    private readonly BackdropNode _backdrop;
    private readonly InvisibleThumb _body;
    private readonly InvisibleThumb[] _corners = new InvisibleThumb[4];
    private readonly InvisibleThumb[] _edges = new InvisibleThumb[4];
    private readonly FrameInfoBadgeNode _infoBadge;

    private Pix2dSprite? _sprite;
    private SKRect _frameRect;
    private SKRect _dragStartFrame;
    private SKPoint _dragStartSpritePos;
    private float _handleWorldSize;
    private ViewPort? _vp;

    /// <summary>Raised when the user presses outside the frame — the service interprets this as "apply".</summary>
    public Action? ApplyRequested { get; set; }

    /// <summary>Called on every live change so the host can refresh the viewport.</summary>
    public Action? OnChanged { get; set; }

    public SKRect FrameRect => _frameRect;

    public ArtboardObjectEditorNode()
    {
        Name = "Artboard object editor";

        _backdrop = new BackdropNode { Pressed = () => ApplyRequested?.Invoke() };
        _body = new InvisibleThumb();
        _body.DragStarted += (_, _) => BeginBodyDrag();
        _body.DragDelta += (_, e) => OnBodyDrag(new SKPoint(e.HorizontalChange, e.VerticalChange));

        Nodes.Add(_backdrop); // bottom: catches clicks outside the frame
        Nodes.Add(_body);     // middle: move the whole artboard

        for (var i = 0; i < 4; i++)
        {
            var corner = (Corner)i;
            var thumb = new InvisibleThumb();
            thumb.DragStarted += (_, _) => BeginResizeDrag();
            thumb.DragDelta += (_, e) => OnCornerDrag(corner, new SKPoint(e.HorizontalChange, e.VerticalChange));
            _corners[i] = thumb;
            Nodes.Add(thumb); // top: corner handles win the hit-test over the body
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

    public void SetTarget(Pix2dSprite sprite)
    {
        _sprite = sprite;
        _frameRect = sprite.GetBoundingBox();
        Layout();
    }

    public void OnViewChanged(ViewPort vp)
    {
        _vp = vp;
        _handleWorldSize = vp.PixelsToWorld(HandleHitPx);
        Layout();
    }

    private void BeginBodyDrag()
    {
        _dragStartFrame = _frameRect;
        _dragStartSpritePos = _sprite?.Position ?? default;
    }

    private void OnBodyDrag(SKPoint delta)
    {
        var dx = MathF.Round(delta.X);
        var dy = MathF.Round(delta.Y);

        _frameRect = new SKRect(_dragStartFrame.Left + dx, _dragStartFrame.Top + dy,
            _dragStartFrame.Right + dx, _dragStartFrame.Bottom + dy);

        if (_sprite != null)
            _sprite.Position = new SKPoint(_dragStartSpritePos.X + dx, _dragStartSpritePos.Y + dy);

        Layout();
        OnChanged?.Invoke();
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

        // Object-edit frame has crop semantics (no rotation), so the rect already is the world-space region.
        return new FrameInfoBadgeNode.FrameInfo(
            _frameRect, new SKPoint(_frameRect.Left, _frameRect.Top), _frameRect.Size, 0);
    }

    private void Layout()
    {
        var hs = _handleWorldSize > 0 ? _handleWorldSize : 22f;

        _body.Position = new SKPoint(_frameRect.Left, _frameRect.Top);
        _body.Size = _frameRect.Size;

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
    private sealed class InvisibleThumb : ThumbNode
    {
        protected override void OnDraw(SKCanvas canvas, ViewPort vp) { }
    }

    /// <summary>Full-viewport catch-all behind the frame: a press here means "apply &amp; exit" and is
    /// marked handled so it never reaches a drawing tool on the root node.</summary>
    private sealed class BackdropNode : SKNode
    {
        public Action? Pressed { get; set; }

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
            Pressed?.Invoke();
        }

        protected override void OnDraw(SKCanvas canvas, ViewPort vp) { }
    }
}
