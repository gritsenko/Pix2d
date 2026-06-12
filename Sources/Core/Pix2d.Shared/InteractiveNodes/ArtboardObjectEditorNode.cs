#nullable enable
using Pix2d.Abstract;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

/// <summary>
/// The interactive overlay shown in "edit sprite as object" mode. Behaviour depends on
/// <see cref="ArtboardObjectEditMode"/>:
/// <list type="bullet">
/// <item><see cref="ArtboardObjectEditMode.Move"/> (default after selection): no handles; the artboard is
/// dragged only by its name label (<see cref="ArtboardLabelsLayer"/> rect). A press on the empty space
/// outside the artboard raises <see cref="BackdropPressed"/> (the service exits the session); a press inside
/// the artboard body is swallowed so it never starts a stray brush stroke.</item>
/// <item><see cref="ArtboardObjectEditMode.Resize"/> / <see cref="ArtboardObjectEditMode.Crop"/>: 4 corner +
/// 4 edge handles edit the working <see cref="FrameRect"/> (frame-only preview — the sprite pixels are not
/// touched until the service applies). Presses outside are swallowed; the user confirms/cancels from
/// SpriteActionsView.</item>
/// </list>
/// The node never commits anything itself — it moves the sprite live during a label drag (and raises
/// <see cref="MoveCompleted"/> so the service can push one undoable move), and exposes <see cref="FrameRect"/>
/// for the service to read when applying a resize/crop.
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
    private readonly InvisibleThumb _labelDrag;
    private readonly InvisibleThumb[] _corners = new InvisibleThumb[4];
    private readonly InvisibleThumb[] _edges = new InvisibleThumb[4];
    private readonly FrameInfoBadgeNode _infoBadge;

    private Pix2dSprite? _sprite;
    private SKRect _frameRect;
    private SKRect _dragStartFrame;
    private SKPoint _dragStartSpritePos;
    private float _handleWorldSize;
    private ViewPort? _vp;

    /// <summary>Mode-dependent press on the empty area outside the artboard. The service exits the session in
    /// <see cref="ArtboardObjectEditMode.Move"/> mode and ignores it while resizing/cropping.</summary>
    public Action? BackdropPressed { get; set; }

    /// <summary>Raised when a label drag ends — the service pushes one undoable move for the whole gesture.</summary>
    public Action? MoveCompleted { get; set; }

    /// <summary>Called on every live change so the host can refresh the viewport.</summary>
    public Action? OnChanged { get; set; }

    public ArtboardObjectEditMode Mode { get; private set; } = ArtboardObjectEditMode.Move;

    public SKRect FrameRect => _frameRect;

    public ArtboardObjectEditorNode()
    {
        Name = "Artboard object editor";

        _backdrop = new BackdropNode { Pressed = OnBackdropPressed };
        _interiorBlocker = new BlockerNode { FrameProvider = () => _frameRect };

        _labelDrag = new InvisibleThumb();
        _labelDrag.DragStarted += (_, _) => BeginMoveDrag();
        _labelDrag.DragDelta += (_, e) => OnMoveDrag(new SKPoint(e.HorizontalChange, e.VerticalChange));
        _labelDrag.DragComplete += (_, _) => MoveCompleted?.Invoke();

        Nodes.Add(_backdrop);          // bottom: catches presses outside the artboard
        Nodes.Add(_interiorBlocker);   // swallows presses inside the artboard body (no draw-through)
        Nodes.Add(_labelDrag);         // Move mode only: drag the artboard by its name label

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

        ApplyModeInteractivity();
    }

    public void SetTarget(Pix2dSprite sprite)
    {
        _sprite = sprite;
        _frameRect = sprite.GetBoundingBox();
        Layout();
    }

    /// <summary>Switches the sub-mode; re-syncs the working frame to the sprite's current bounds so a
    /// resize/crop gesture always starts from the committed state.</summary>
    public void SetMode(ArtboardObjectEditMode mode)
    {
        Mode = mode;
        if (_sprite != null)
            _frameRect = _sprite.GetBoundingBox();
        ApplyModeInteractivity();
        Layout();
        OnChanged?.Invoke();
    }

    private void ApplyModeInteractivity()
    {
        var editing = Mode != ArtboardObjectEditMode.Move;
        _labelDrag.IsInteractive = !editing;
        foreach (var t in _corners) t.IsInteractive = editing;
        foreach (var t in _edges) t.IsInteractive = editing;
    }

    public void OnViewChanged(ViewPort vp)
    {
        _vp = vp;
        _handleWorldSize = vp.PixelsToWorld(HandleHitPx);
        Layout();
    }

    private void OnBackdropPressed()
    {
        // Outside-the-artboard press: exit only in Move mode; swallowed while resizing/cropping.
        if (Mode == ArtboardObjectEditMode.Move)
            BackdropPressed?.Invoke();
    }

    private void BeginMoveDrag()
    {
        _dragStartFrame = _frameRect;
        _dragStartSpritePos = _sprite?.Position ?? default;
    }

    private void OnMoveDrag(SKPoint delta)
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
        // The badge only makes sense while resizing/cropping (Move mode has no size readout to show).
        if (Mode == ArtboardObjectEditMode.Move || _frameRect.Width <= 0 || _frameRect.Height <= 0)
            return null;

        // Object-edit frame has no rotation, so the rect already is the world-space region.
        return new FrameInfoBadgeNode.FrameInfo(
            _frameRect, new SKPoint(_frameRect.Left, _frameRect.Top), _frameRect.Size, 0);
    }

    private void Layout()
    {
        var hs = _handleWorldSize > 0 ? _handleWorldSize : 22f;

        // Label drag handle sits over the artboard's name label (drag-by-label); zero-sized when not editing
        // by move, but the hit-test is gated by IsInteractive anyway.
        if (_vp != null && _sprite != null)
        {
            var label = ArtboardLabelsLayer.GetLabelRect(_vp, _sprite);
            _labelDrag.Position = label.Location;
            _labelDrag.Size = label.Size;
        }

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

        // Move mode shows no frame of its own — the active-artboard highlight border (drawn by Pix2dSprite)
        // and the cyan name label are enough; only resizing/cropping needs the working frame + handles.
        if (Mode == ArtboardObjectEditMode.Move)
            return;

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

    /// <summary>Full-viewport catch-all behind the frame: a press here is forwarded to <see cref="Pressed"/>
    /// and marked handled so it never reaches a drawing tool on the root node.</summary>
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
