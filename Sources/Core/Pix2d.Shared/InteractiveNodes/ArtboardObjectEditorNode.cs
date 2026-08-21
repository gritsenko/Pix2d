#nullable enable
using Pix2d.Abstract;
using Pix2d.CommonNodes;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Extensions;
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
/// The frame is not the only feedback: each sub-mode previews its *result* live while the handles are
/// dragged, Photoshop-style, because a moving rectangle alone says nothing about what the pixels will do.
/// <list type="bullet">
/// <item><b>Resize</b> — a snapshot of the artboard is drawn stretched into the frame (nearest-neighbour, on
/// the same checkerboard the canvas uses). The real sprite is render-suppressed for the session by the
/// service, so the stand-in *is* the artboard on screen: shrinking the frame vacates scene background
/// instead of leaving the original showing around the preview. See <see cref="PreviewsTargetContent"/>.</item>
/// <item><b>Crop</b> — the sprite keeps painting itself and everything outside the frame is dimmed by a crop
/// shield, so the frame reads as "the part that survives".</item>
/// </list>
///
/// Moving an artboard is deliberately not handled here — that is a plain drag in the General context
/// (<c>ObjectManipulationTool</c> + the object selection frame).
/// </summary>
public class ArtboardObjectEditorNode : SKNode, IViewPortBindable, IDisposable
{
    private enum Corner { LeftTop, RightTop, RightBottom, LeftBottom }
    private enum Edge { Top, Right, Bottom, Left }

    private const float HandleHitPx = 22f;     // grab area
    private const float HandleVisualPx = 11f;  // drawn square
    private static readonly SKColor Accent = new(0x29, 0xB0, 0xF3);

    /// <summary>Dim applied outside a Crop frame. Dark enough to read as "this goes away", light enough that
    /// the content being trimmed stays recognizable.</summary>
    private static readonly SKColor CropShieldColor = new(0x00, 0x00, 0x00, 0x99);

    /// <summary>Pixel art must stay pixel art while it is being stretched — no smoothing, no mip chain.</summary>
    private static readonly SKSamplingOptions PreviewSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    private readonly BackdropNode _backdrop;
    private readonly BlockerNode _interiorBlocker;
    private readonly InvisibleThumb[] _corners = new InvisibleThumb[4];
    private readonly InvisibleThumb[] _edges = new InvisibleThumb[4];
    private readonly FrameInfoBadgeNode _infoBadge;

    private Pix2dSprite? _sprite;
    private SKRect _frameRect;
    private SKRect _dragStartFrame;
    private float _dragStartAspect = 1f;
    private float _handleWorldSize;
    private ViewPort? _vp;

    // Resize preview: a 1:1 snapshot of the artboard taken once, when the session opens. The bitmap is kept
    // alive next to the image because SKImage.FromBitmap shares its pixel ref (see BitmapNode's mip cache).
    private SKBitmap? _previewBitmap;
    private SKImage? _previewImage;
    private SKColor? _previewBackground;

    /// <summary>Called on every live change so the host can refresh the viewport.</summary>
    public Action? OnChanged { get; set; }

    /// <summary>
    /// Proportional lock for the handle drags: while it is on, a corner/edge drag keeps the aspect ratio the
    /// frame had when the drag started. The service sets the default per sub-mode (on for Resize — scaling
    /// artwork non-uniformly is the exception, not the rule; off for Crop, where an arbitrary region is the
    /// point) and the action bar's toggle drives it afterwards.
    /// </summary>
    public bool KeepAspect { get; set; }

    /// <summary>
    /// The lock actually in force for the gesture in flight: <b>Shift inverts</b> <see cref="KeepAspect"/>,
    /// the same modifier convention as <c>SnappingService.IsAspectLocked</c>. Read live on every move, so the
    /// modifier can be pressed or released mid-drag.
    /// </summary>
    private bool IsAspectLocked =>
        KeepAspect ^ SKInput.Current.GetModifiers().HasFlag(KeyModifier.Shift);

    public ArtboardObjectEditMode Mode { get; private set; } = ArtboardObjectEditMode.Resize;

    public SKRect FrameRect => _frameRect;

    /// <summary>
    /// True when this overlay draws the target artboard's content itself (Resize, snapshot captured OK) and
    /// the real node must therefore be render-suppressed while the session is open. False for Crop, and for a
    /// Resize whose snapshot could not be taken — in which case the frame degrades to outline-only rather
    /// than blanking the artboard.
    /// </summary>
    public bool PreviewsTargetContent => _previewImage != null;

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

        if (mode == ArtboardObjectEditMode.Resize)
            CapturePreview(sprite);

        Layout();
    }

    /// <summary>
    /// Sets the working frame's size directly — the numeric inputs in the action bar — keeping its top-left
    /// pinned, so typing a size grows/shrinks the artboard the same way dragging the bottom-right handle
    /// does. Whole pixels only and clamped to the canvas limits (<see cref="CanvasSize"/>), because these
    /// values come straight from a text box. Still preview-only: this is the same <see cref="FrameRect"/> the
    /// service applies on confirm.
    /// </summary>
    public void SetFrameSize(SKSize size)
    {
        var sanitized = CanvasSize.Sanitize(new SKSize(MathF.Round(size.Width), MathF.Round(size.Height)));
        if (sanitized == _frameRect.Size)
            return;

        _frameRect = SKRect.Create(_frameRect.Left, _frameRect.Top, sanitized.Width, sanitized.Height);
        Layout();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Grabs the artboard's current pixels once, at 1:1, as the source of the stretched Resize preview
    /// (RenderToBitmap runs with RenderAdorners off, so no checkerboard / highlight / onion skin leaks in).
    /// A failure here is not fatal: the session simply keeps the old outline-only behaviour.
    /// </summary>
    private void CapturePreview(Pix2dSprite sprite)
    {
        try
        {
            _previewBitmap = new SKNode[] { sprite }.RenderToBitmap();
            _previewImage = SKImage.FromBitmap(_previewBitmap);
            _previewBackground = sprite.UseBackgroundColor && sprite.BackgroundColor != default
                ? sprite.BackgroundColor
                : null;
        }
        catch (Exception ex)
        {
            // Oversized canvas / out of memory — RenderToBitmap reports both as InvalidOperationException.
            Logger.LogException(ex);
            Logger.Trace($"Artboard resize preview unavailable ({ex.Message}) — frame-only fallback");
            ReleasePreview();
        }
    }

    private void ReleasePreview()
    {
        _previewImage?.Dispose();
        _previewImage = null;
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        _previewBackground = null;
    }

    public void Dispose() => ReleasePreview();

    public void OnViewChanged(ViewPort vp)
    {
        _vp = vp;
        _handleWorldSize = vp.PixelsToWorld(HandleHitPx);
        Layout();
    }

    private void BeginResizeDrag()
    {
        _dragStartFrame = _frameRect;
        // Lock to the ratio the frame has *now*, not the artboard's original: after an unlocked drag the
        // user expects a following locked drag to keep what is on screen.
        _dragStartAspect = _frameRect.Height > 0 ? _frameRect.Width / _frameRect.Height : 1f;
    }

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

        var movesLeft = corner is Corner.LeftTop or Corner.LeftBottom;
        var movesTop = corner is Corner.LeftTop or Corner.RightTop;

        if (IsAspectLocked && _dragStartAspect > 0)
        {
            var w = right - left;
            var h = bottom - top;

            // Follow whichever axis the pointer took further (compared in ratio-normalized terms), so a
            // diagonal drag tracks the cursor instead of fighting it.
            if (MathF.Abs(w - f.Width) >= MathF.Abs(h - f.Height) * _dragStartAspect)
                h = w / _dragStartAspect;
            else
                w = h * _dragStartAspect;

            // The corner opposite the dragged one stays pinned, exactly as in the unlocked case.
            if (movesLeft) left = right - w; else right = left + w;
            if (movesTop) top = bottom - h; else bottom = top + h;
        }

        _frameRect = NormalizeFrame(left, top, right, bottom, movesLeft, movesTop);
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

        if (IsAspectLocked && _dragStartAspect > 0)
        {
            // The dragged edge sets one dimension; the cross axis follows the ratio, grown symmetrically
            // about the frame's centre line so the frame scales in place instead of drifting to one side.
            if (edge is Edge.Left or Edge.Right)
            {
                var h = (right - left) / _dragStartAspect;
                top = f.MidY - h / 2f;
                bottom = f.MidY + h / 2f;
            }
            else
            {
                var w = (bottom - top) * _dragStartAspect;
                left = f.MidX - w / 2f;
                right = f.MidX + w / 2f;
            }
        }

        _frameRect = NormalizeFrame(left, top, right, bottom, edge == Edge.Left, edge == Edge.Top);
        Layout();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Rounds a working rect to whole pixels and guarantees a >= 1px canvas in both axes. The flags name the
    /// edge that may give way — the un-dragged edge always stays pinned, so a handle dragged past its
    /// opposite edge stops there instead of inverting the frame. Both axes are checked whatever handle
    /// started the drag, because an aspect-locked gesture also drives the cross axis.
    /// </summary>
    private static SKRect NormalizeFrame(float left, float top, float right, float bottom,
        bool movesLeft, bool movesTop)
    {
        left = MathF.Round(left);
        top = MathF.Round(top);
        right = MathF.Round(right);
        bottom = MathF.Round(bottom);

        if (right - left < 1)
        {
            if (movesLeft) left = right - 1;
            else right = left + 1;
        }
        if (bottom - top < 1)
        {
            if (movesTop) top = bottom - 1;
            else bottom = top + 1;
        }

        return new SKRect(left, top, right, bottom);
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

        // The result preview goes under the frame chrome — handles and the info badge must stay readable
        // on top of a stretched image / a dimmed background.
        DrawResultPreview(canvas, vp);

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

    /// <summary>
    /// Draws what the frame will actually produce: the stretched artboard for Resize, the crop shield for
    /// Crop. Nothing is committed — this is the same <see cref="FrameRect"/> the service later applies.
    /// </summary>
    private void DrawResultPreview(SKCanvas canvas, ViewPort vp)
    {
        if (_frameRect.Width <= 0 || _frameRect.Height <= 0)
            return;

        if (Mode == ArtboardObjectEditMode.Crop)
        {
            // Photoshop's crop shield: dim everything on screen except the region that survives the crop.
            // Union with the frame so a frame dragged past the visible area still carves its hole cleanly.
            var shielded = vp.GetVisibleArea();
            shielded.Union(_frameRect);

            using var shield = new SKPaint { Color = CropShieldColor };
            canvas.Save();
            canvas.ClipRect(_frameRect, SKClipOperation.Difference);
            canvas.DrawRect(shielded, shield);
            canvas.Restore();
            return;
        }

        if (_previewImage == null)
            return;

        // The sprite is render-suppressed for the session (PreviewsTargetContent), so the canvas background
        // it would have painted has to come from here too — otherwise transparent pixels would show the
        // scene background instead of the checkerboard.
        if (_previewBackground is { } background)
        {
            using var fill = new SKPaint { Color = background };
            canvas.DrawRect(_frameRect, fill);
        }
        else
        {
            CanvasCheckerboard.Draw(canvas, vp, _frameRect);
        }

        canvas.DrawImage(_previewImage, _frameRect, PreviewSampling);
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
