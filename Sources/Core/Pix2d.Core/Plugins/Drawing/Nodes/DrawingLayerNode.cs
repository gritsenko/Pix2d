using System.Diagnostics;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Selection;
using Pix2d.InteractiveNodes;
using Pix2d.Plugins.Drawing.Brushes;
using Pix2d.Plugins.Drawing.Common;
using Pix2d.Plugins.Drawing.Common.Drawing;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Plugins.Drawing.PixelSelectors;
using Pix2d.Primitives;
using Pix2d.Primitives.Drawing;
using Pix2d.Primitives.Edit;
using Pix2d.Selection;
using Pix2d.Services;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

public class DrawingLayerNode : SKNode, IDrawingLayer, IPixelSelectionEditor, ISelectionLayerHost, IStrokeRendererHost, IPointerInputRouterHost
{

    public event EventHandler? DrawingStarted;
    public event EventHandler<DrawingAppliedEventArgs>? DrawingApplied;
    public event EventHandler? LayerModified;

    // Selection events live on SelectionController. We re-raise them with `this` as sender so
    // existing consumers (PixelSelectToolBase, ExtractObjectTool, DrawingService, PixelTextTool)
    // keep seeing the drawing layer node, not the controller.
    public event EventHandler? SelectionStarted;
    public event EventHandler? SelectionRemoved;
    public event EventHandler? PixelsSelected;
    public event EventHandler<PixelsBeforeSelectedEventArgs>? PixelsBeforeSelected;
    public event EventHandler<SelectionTransformedEventArgs>? SelectionTransformed;

    /// <summary>
    /// Fires from the underlying <see cref="SelectionController.FinishSelection"/> after a fresh marquee
    /// is fully set up. Distinct from <see cref="PixelsSelected"/> which also fires on every
    /// <c>SetSelection</c> call (paste / undo / redo); this one specifically marks the user just
    /// finishing a new marquee gesture, which is the point at which <c>BeginSelectionOperation</c>
    /// should be pushed onto the undo stack.
    /// </summary>
    public event EventHandler? MarqueeFinishedByUser;

    private SKBitmap? _backgroundBitmap;
    private SKBitmap? _swapBitmap;
    private SKBitmap? _workingBitmap;

    // Immutable copy-on-write view of _workingBitmap handed to the compositor. While set, OnDraw
    // draws this instead of the live bitmap. Avalonia composites on a separate render thread, and
    // canvas.DrawBitmap reads pixel memory during flush; if the UI thread writes to the bitmap in
    // that window the user sees torn horizontal bands. SKImage.FromBitmap is allocation-free in the
    // steady state (shares the SkPixelRef) and Skia forks on the next write — so the compositor
    // keeps reading the snapshot's original bytes while UI thread renders the next frame freely.
    // Used by SelectionController during a transform drag (write rate >> paint rate) and cleared
    // when the editor deactivates.
    //
    // Access is guarded by _snapshotLock: OnDraw runs on Avalonia's render thread while
    // PromoteWorkingBitmapToDisplay / ClearDisplaySnapshot run on the UI thread. Without the lock,
    // the UI thread could Dispose() the SKImage between the render thread reading the field and
    // calling canvas.DrawImage on it — at which point the handle is IntPtr.Zero and SkiaSharp
    // throws. DrawImage takes its own sk_sp ref on the native side, so dispose-after-DrawImage is
    // safe; the lock only needs to cover the field read + the DrawImage call itself.
    private SKImage? _workingBitmapDisplaySnapshot;
    private SKImage? _swapBitmapDisplaySnapshot;
    private readonly Lock _snapshotLock = new();

    public bool UseSwapBitmap { get; set; }

    /// <summary>
    /// The layer's bitmap currently displayed on the screen. All drawing and selection operations are rendered to
    /// this bitmap until they are applied to the <see cref="DrawingTarget"/>.
    /// </summary>
    public SKBitmap WorkingBitmap => (UseSwapBitmap ? _swapBitmap : _workingBitmap) ?? throw new InvalidOperationException("WorkingBitmap is not initialized");

    private BrushDrawingMode _drawingMode;
    private readonly SelectionController _selection;
    private readonly StrokeRenderer _strokeRenderer;
    private readonly PointerInputRouter _pointerInputRouter;
    private readonly SymmetryOverlayNode _symmetryOverlay = new();
    private SymmetrySettings _symmetry = SymmetrySettings.Off;

    private SKColor _drawingColor;
    private float _fillOpacity = 1f;
    private IPixelBrush? _brush;
    // Written on the UI thread (UpdateBrushPreview — every color / brush / size change) and read on
    // Avalonia's render thread (RenderBrushPreview → canvas.DrawSurface). The lock covers the field read
    // plus the DrawSurface calls, so the UI thread can never dispose the surface the render thread is
    // drawing: that was a native use-after-free which killed the process with no managed exception
    // (issue #253 — a crash "every once in a while" while dragging the color slider, which pushes one
    // color change per pointer-move event). Same shape as _snapshotLock; the two are never nested.
    private SKSurface? _brushPreviewSurface;
    private readonly Lock _brushPreviewLock = new();

    // Render-thread anchor buffer for the preview's symmetry copies. Kept here, and touched only under
    // _brushPreviewLock, so the render thread never shares StrokeRenderer's stroke buffer with the UI
    // thread rasterizing into the same layer — see the note on StrokeRenderer._symmetryImages.
    private readonly List<SKPointI> _previewSymmetryImages = [];

    // Live stylus pressure [0..1] for the current pointer event, captured on press/move and fed to the
    // brush on each freehand stamp. Defaults to 1 (full pressure) for mouse/touch and between strokes.
    private double _currentPressure = 1;
    private readonly List<SKPointI> _strokePoints = new();
    private readonly List<SKPointI> _pixelPerfectPreviewPoints = new();

    // Even-opacity ("Opacity vs Flow") + smoothing for soft brushes (IPixelBrush.StrokeStyle). When the
    // current stroke is smooth, dabs are unioned into the working bitmap at full strength and the whole
    // bitmap is composited once at _strokeBufferOpacity (the brush opacity); when null the working bitmap is
    // composited opaque, i.e. legacy per-dab-opacity stamping (hard pixel brushes) — pixel-for-pixel identical.
    private float? _strokeBufferOpacity;
    private bool _useSmoothStroke;
    private SKPoint _smoothPos;            // streamlined cursor position, layer space
    private SKPoint _lastSmoothStampPos;   // last placed dab center, layer space
    private float _smoothSpacingLeftover;  // partial gap carried between pointer events
    private bool _smoothStrokeStarted;

    // Single-pole low-pass on the cursor path: each pointer event the working position eases this fraction
    // toward the raw cursor. 1 = no smoothing; lower = smoother but laggier. Tuned light so it removes hand
    // jitter without feeling disconnected in a pixel editor.
    private const float SmoothStrokeFollow = 0.6f;

    public bool HasSelectionChanges => _selection.HasSelectionChanges;

    public bool IsPixelPerfectMode { get; set; }

    public SKColor DrawingColor
    {
        get => _drawingColor;
        set
        {
            if (_drawingColor != value)
            {
                _drawingColor = value;
                UpdateBrushPreview(_brush);
            }
        }
    }

    /// <inheritdoc />
    public float FillOpacity
    {
        get => _fillOpacity;
        set => _fillOpacity = Math.Clamp(value, 0f, 1f);
    }

    public IPixelBrush Brush
    {
        get => _brush!;
        set
        {
            _brush = value;
            UpdateBrushPreview(_brush);
        }
    }

    public DrawingLayerState State { get; set; }

    /// <summary>
    /// Currently active application layer that the user works with. After drawing or selection operations are done
    /// they are applied to the DrawingTarget.
    /// </summary>
    public IDrawingTarget? DrawingTarget { get; private set; }

    public PixelSelectionMode SelectionMode
    {
        get => _selection.SelectionMode;
        set => _selection.SelectionMode = value;
    }

    public int ColorSelectionTolerance { get; set; }

    public ColorSelectionScope ColorSelectionScope { get; set; } = ColorSelectionScope.Connected;

    public bool HasSelection => _selection.HasSelection;

    public SelectionPhase SelectionPhase => _selection.SelectionPhase;

    /// <inheritdoc />
    public SymmetrySettings Symmetry
    {
        get => _symmetry;
        set
        {
            _symmetry = value;
            _symmetryOverlay.Settings = value;
        }
    }

    /// <summary>
    /// Raised when the user drags (or double-clicks to reset) the on-canvas symmetry handle. Null means
    /// "back to the middle of the canvas". <see cref="Pix2d.Abstract.Services.IDrawingService"/> owns the
    /// state, so the node only reports the gesture.
    /// </summary>
    public Action<SKPoint?>? SymmetryCenterChanged
    {
        get => _symmetryOverlay.CenterChanged;
        set => _symmetryOverlay.CenterChanged = value;
    }

    public bool LockTransparentPixels => DrawingTarget?.LockTransparentPixels ?? false;
    public bool ShowBrushPreview { get; set; }

    public SKSize SelectionSize
    {
        get => _selection.SelectionSize;
        private set => _selection.SelectionSize = value;
    }

    private SKPoint LastPointerPosition
    {
        get => _pointerInputRouter.LastPointerPosition;
        set => _pointerInputRouter.LastPointerPosition = value;
    }

    private SKPointI PreviewPosition
    {
        get => _pointerInputRouter.PreviewPosition;
        set => _pointerInputRouter.PreviewPosition = value;
    }

    private SKPointI StartPosI => StartPos.ToSkPointI();
    private SKPointI EndPosI => EndPos.ToSkPointI();
    public SKNode GetSelectionLayer() => _selection.GetSelectionLayerNode();
    public IAspectSnapper? AspectSnapper { get; set; }

    /// <summary>
    /// Snapshot of the currently-active tool key. Used by selection-related operations so that
    /// <c>Undo</c>/<c>Redo</c> can restore the right tool and keep UI/drawing-layer state consistent
    /// (see <see cref="IToolAwareOperation"/>).
    /// </summary>
    public Func<string?>? ActiveToolKeyProvider { get; set; }

    /// <summary>
    /// Click-to-activate gate for multi-artboard scenes. Invoked on a pointer press (before any
    /// <see cref="CapturePointer"/> / <see cref="BeginDrawing"/>) with the world position; returns
    /// <c>true</c> when the press landed on a different artboard — which the resolver activates — so the
    /// current gesture is swallowed and no stroke is started on the outgoing sprite. Set by
    /// <c>DrawingService</c>. The check lives here rather than in the brush tool because this node
    /// receives the press first in the input dispatch and would otherwise begin drawing immediately.
    /// </summary>
    public Func<SKPoint, bool>? ArtboardActivationResolver { get; set; }

    public AxisLockMode AxisLockMode
    {
        get => _pointerInputRouter.AxisLockMode;
        set => _pointerInputRouter.AxisLockMode = value;
    }

    private bool IsInitialized => DrawingTarget != null && _backgroundBitmap != null && _workingBitmap != null;

    public SKBitmap GetSelectionBackground() => _selection.GetSelectionBackground();

    public DrawingLayerNode()
    {
        IsInteractive = true;

        _selection = new SelectionController(this);
        _strokeRenderer = new StrokeRenderer(this);
        _pointerInputRouter = new PointerInputRouter(this);

        // A child rather than a sibling adorner: it inherits this node's coordinate space, so the axes are
        // drawn straight in canvas pixels, and being deeper in the tree it sees a press before the drawing
        // layer does (SKInput walks visible descendants and reverses them).
        Nodes.Add(_symmetryOverlay);

        // Re-raise selection events through the node so external consumers (PixelSelectToolBase,
        // ExtractObjectTool, DrawingService, PixelTextTool) keep seeing the node as event sender.
        _selection.SelectionStarted     += (_, e) => SelectionStarted?.Invoke(this, e);
        _selection.SelectionRemoved     += (_, e) => SelectionRemoved?.Invoke(this, e);
        _selection.PixelsSelected       += (_, e) => PixelsSelected?.Invoke(this, e);
        _selection.PixelsBeforeSelected += (_, e) => PixelsBeforeSelected?.Invoke(this, e);
        _selection.SelectionTransformed += (_, e) => SelectionTransformed?.Invoke(this, e);
        _selection.MarqueeFinishedByUser += (_, e) => MarqueeFinishedByUser?.Invoke(this, e);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        // The overlay resolves "centre of the canvas" and clips its axes against this size.
        _symmetryOverlay.Size = Size;
    }

    private void ClearWorkingBitmap()
    {
        _backgroundBitmap?.Clear();
        _workingBitmap?.Clear();
        _swapBitmap?.Clear();
        ClearDisplaySnapshot();
    }

    private void PublishDisplaySnapshot()
    {
        PublishBitmapSnapshot(_workingBitmap, ref _workingBitmapDisplaySnapshot);
    }

    private void PublishSwapBitmapDisplaySnapshot()
    {
        PublishBitmapSnapshot(_swapBitmap, ref _swapBitmapDisplaySnapshot);
    }

    private void PublishBitmapSnapshot(SKBitmap? bitmap, ref SKImage? snapshot)
    {
        if (bitmap == null) return;
        // Build the new image outside the lock to keep the render-thread critical section short.
        var newImage = SKImage.FromBitmap(bitmap);
        SKImage? old;
        lock (_snapshotLock)
        {
            old = snapshot;
            snapshot = newImage;
        }
        old?.Dispose();
    }

    private void ClearDisplaySnapshot()
    {
        ClearBitmapSnapshot(ref _workingBitmapDisplaySnapshot);
        ClearBitmapSnapshot(ref _swapBitmapDisplaySnapshot);
    }

    private void ClearSwapBitmapDisplaySnapshot()
    {
        ClearBitmapSnapshot(ref _swapBitmapDisplaySnapshot);
    }

    private void ClearBitmapSnapshot(ref SKImage? snapshot)
    {
        SKImage? old;
        lock (_snapshotLock)
        {
            old = snapshot;
            snapshot = null;
        }
        old?.Dispose();
    }

    public override bool ContainsPoint(SKPoint worldPos)
    {
        return true;
    }

    public override void OnPanModeChanged(bool isPanModeEnabled)
    {
        if (_drawingMode == BrushDrawingMode.Draw)
            CancelDrawing();

        // Pinch/pan gesture started before the user actually began a new selection:
        // drop the deferred start so the existing selection is preserved.
        _pointerInputRouter.OnPanModeChanged(isPanModeEnabled);

        //if (_drawingMode == BrushDrawingMode.Select)
        //    CancelSelect();
    }

    public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
        if (!IsInitialized) return;

        _currentPressure = eventArgs.Pointer.Pressure;

        // On touch screens move event might not capture last position, so we need to update it here
        // to prevent bugs with axis locked drawing.
        PreviewPosition = eventArgs.Pointer.GetPosition(this).ToSkPointI();

        if (_pointerInputRouter.ShouldIgnorePointerPressed(eventArgs))
            return;

        // Click-to-activate artboard: a press on a different sprite switches the active edit target and
        // swallows this gesture, so the outgoing artboard never receives a stray stroke. Runs before the
        // capture/BeginDrawing below — this is the earliest point in the press dispatch we control.
        if (ArtboardActivationResolver?.Invoke(eventArgs.Pointer.WorldPosition) == true)
            return;

        base.OnPointerPressed(eventArgs, clickCount);

        LastPointerPosition = eventArgs.Pointer.WorldPosition.ToSkPointI();
        _pointerInputRouter.HandlePointerPressed(eventArgs);
    }

    public override void OnPointerReleased(PointerActionEventArgs eventArgs)
    {
        if (!IsInitialized) return;

        ReleasePointerCapture();

        if (_pointerInputRouter.TryHandleDeferredTouchTapRelease())
        {
            base.OnPointerReleased(eventArgs);
            return;
        }

        try
        {
            _pointerInputRouter.HandlePointerReleased(eventArgs);
        }
        finally
        {
            base.OnPointerReleased(eventArgs);
        }
    }

    private void SwapWorkingBitmap()
    {
        (_swapBitmap, _workingBitmap) = (_workingBitmap, _swapBitmap);
        _swapBitmap?.Clear();

        Refresh();
    }

    public override void OnPointerMoved(PointerActionEventArgs eventArgs)
    {
        if (!IsInitialized) return;

        _currentPressure = eventArgs.Pointer.Pressure;

        var prevPointerPosition = PreviewPosition;
        var currPointerPosition = eventArgs.Pointer.GetPosition(this).ToSkPointI();

        PreviewPosition = currPointerPosition;

        if (_drawingMode == BrushDrawingMode.ExternalDraw)
        {
            // Shape tools drive their own drawing from the tool, so there is no stroke to advance here —
            // but this node still owns the brush cursor overlay, and it has to repaint as the pointer
            // moves. Without this the cursor sat frozen wherever it was last drawn and only caught up
            // when some unrelated event refreshed the viewport (crossing an artboard boundary, say).
            if (currPointerPosition != prevPointerPosition && IsShowingBrush())
                Refresh();

            return;
        }

        base.OnPointerMoved(eventArgs);

        if (eventArgs.Pointer.IsPressed || eventArgs.Pointer.IsEraser)
        {
            _pointerInputRouter.HandlePointerMoved(eventArgs, prevPointerPosition, currPointerPosition);
        }

        Refresh();
    }

    private void FinishReleasedDrawing()
    {
        // A tap (press+release with no move) commits its single point here; keep its pressure consistent
        // with the freehand path above.
        if (_brush != null)
            _brush.CurrentPressure = _currentPressure;

        // Smooth strokes lag the cursor; lay the tail down to the release point before finishing.
        FlushSmoothStroke();

        if (IsPixelPerfectMode && _strokePoints.Count > 1)
        {
            if (UseSwapBitmap)
            {
                var ppf = PixelPerfect(_strokePoints);
                DrawStroke(ppf);
                SwapWorkingBitmap();
            }
            else
            {
                CommitPixelPerfectPreviewTailToWorkingBitmap();
                ClearPixelPerfectPreviewTail();
            }
        }

        if (State == DrawingLayerState.Drawing && StartPosI == EndPosI)
        {
            if (_drawingMode == BrushDrawingMode.Draw)
            {
                DrawPointStroke(EndPosI, Brush, DrawingColor, Opacity, 1);
            }
            else
            {
                _strokeRenderer.ErasePoint(Brush, EndPosI, 1);
            }
        }

        if (State != DrawingLayerState.Ready)
        {
            FinishDrawing();
        }
    }


    private void DrawStroke(IEnumerable<SKPointI> path)
    {
        var pp = path.ToArray();
        if (pp.Length == 0)
            return;

        if (pp.Length == 1)
        {
            if (_drawingMode == BrushDrawingMode.Draw)
                DrawPointStroke(new SKPoint(pp[0].X, pp[0].Y), Brush, DrawingColor, Opacity, 1);
            else if (_drawingMode == BrushDrawingMode.Erase)
                _strokeRenderer.ErasePoint(Brush, pp[0], 1);
            return;
        }

        var lp = pp[0];
        for (var i = 1; i < pp.Length; i++)
        {
            var pos = pp[i];
            if (_drawingMode == BrushDrawingMode.Draw)
                DrawStroke(lp, pos, Brush, DrawingColor, 1);

            if (_drawingMode == BrushDrawingMode.Erase)
                EraseStroke(lp, pos, Brush, 1);

            lp = pos;
        }
    }


    private void DrawStroke(SKPoint pos)
    {
        // Feed the live stylus pressure into the brush for this freehand stamp. Shapes never reach this
        // path and BeginDrawing resets it to 1, so non-freehand drawing keeps full pressure.
        if (_brush != null)
            _brush.CurrentPressure = _currentPressure;

        if (_drawingMode == BrushDrawingMode.Draw)
        {
            if (IsPixelPerfectMode)
                DrawPixelPerfect(pos);
            else if (_useSmoothStroke)
                DrawSmoothStroke(pos);
            else
                DrawStroke(LastPointerPosition, pos, Brush, DrawingColor, 1);
        }

        if (_drawingMode == BrushDrawingMode.Erase)
            EraseStroke(LastPointerPosition, pos, Brush, 1);

        LastPointerPosition = pos;
    }

    // --- Smooth soft-brush freehand stroke (IPixelBrush.StrokeStyle Airbrush/Marker) ------------------
    // Streamlines the raw cursor path and lays dabs at even arc-length spacing in layer space, bypassing the
    // brush's integer Bresenham + integer-distance spacing. Together with the even-opacity stroke buffer
    // (_strokeBufferOpacity) this produces a smooth, uniform Photoshop/Sketchbook-style stroke instead of
    // the beaded line you get from compositing many low-opacity dabs directly.
    private void DrawSmoothStroke(SKPoint worldPos)
    {
        if (!GetGlobalTransform().TryInvert(out var inverted))
            return;

        var target = inverted.MapPoint(worldPos);

        if (!_smoothStrokeStarted)
        {
            _smoothPos = target;
            _lastSmoothStampPos = target;
            _smoothSpacingLeftover = 0f;
            _smoothStrokeStarted = true;
            StampSmooth(target); // anchor dab so a tap / very short stroke still leaves a mark
            return;
        }

        _smoothPos = new SKPoint(
            _smoothPos.X + (target.X - _smoothPos.X) * SmoothStrokeFollow,
            _smoothPos.Y + (target.Y - _smoothPos.Y) * SmoothStrokeFollow);
        EmitSmoothStamps(_lastSmoothStampPos, _smoothPos);
    }

    // Lays dabs every `spacing` units along from→to, carrying the partial gap across calls so spacing stays
    // continuous over the whole stroke regardless of how the pointer events were chunked.
    private void EmitSmoothStamps(SKPoint from, SKPoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-3f)
            return;

        var spacing = MathF.Max(1f, ((BasePixelBrush)Brush).AbsoluteSpacing);
        var dirX = dx / dist;
        var dirY = dy / dist;

        var d = spacing - _smoothSpacingLeftover; // distance into this segment for the first dab
        var lastPlaced = d - spacing;             // tracks last dab offset even when none is placed this call
        while (d <= dist)
        {
            StampSmooth(new SKPoint(from.X + dirX * d, from.Y + dirY * d));
            lastPlaced = d;
            d += spacing;
        }

        _smoothSpacingLeftover = dist - lastPlaced; // partial gap carried into the next segment
        _lastSmoothStampPos = to;
    }

    private void StampSmooth(SKPoint layerPos)
    {
        var p = new SKPointI((int)MathF.Round(layerPos.X), (int)MathF.Round(layerPos.Y));
        _strokeRenderer.StampPoint(Brush, p, DrawingColor, 1);
    }

    // Reaches the release point: streamline lags behind the cursor, so on lift we lay the remaining dabs
    // straight to the last raw position and anchor the very end.
    private void FlushSmoothStroke()
    {
        if (!_useSmoothStroke || !_smoothStrokeStarted)
            return;

        if (GetGlobalTransform().TryInvert(out var inverted))
        {
            var end = inverted.MapPoint(EndPos);
            EmitSmoothStamps(_lastSmoothStampPos, end);
            StampSmooth(end);
        }

        _smoothStrokeStarted = false;
    }

    private void DrawPixelPerfect(SKPoint pos)
    {
        var intpos = pos.ToSkPointI();
        if (_strokePoints.Count > 0 && intpos == _strokePoints[^1])
            return;

        _strokePoints.Add(intpos);

        if (!UseSwapBitmap)
        {
            UpdatePixelPerfectPreviewIncremental(intpos);
            Refresh();
            return;
        }

        DrawStroke(PixelPerfect(_strokePoints));
        SwapWorkingBitmap();
        // Pixel-perfect pencil also presents a live preview by swapping buffers on every move.
        // Publish the same stable snapshot used by shape/transform previews so the compositor
        // never races the next stroke update against the just-swapped bitmap.
        PublishDisplaySnapshot();
    }

    private void UpdatePixelPerfectPreviewIncremental(SKPointI point)
    {
        if (_pixelPerfectPreviewPoints.Count == 0)
        {
            _pixelPerfectPreviewPoints.Add(point);
            RedrawPixelPerfectPreviewTail();
            return;
        }

        var shouldSkipPreviousRenderedPoint = _strokePoints.Count >= 3
            && ShouldSkipPixelPerfectPoint(_strokePoints[^3], _strokePoints[^2], point);

        if (!shouldSkipPreviousRenderedPoint)
        {
            if (_pixelPerfectPreviewPoints.Count > 1)
            {
                CommitPixelPerfectPreviewTailToWorkingBitmap();
            }

            _pixelPerfectPreviewPoints.Add(point);
        }
        else
        {
            _pixelPerfectPreviewPoints[^1] = point;
        }

        RedrawPixelPerfectPreviewTail();
    }

    private void CommitPixelPerfectPreviewTailToWorkingBitmap()
    {
        if (_pixelPerfectPreviewPoints.Count == 0)
            return;

        if (_pixelPerfectPreviewPoints.Count == 1)
        {
            var point = _pixelPerfectPreviewPoints[0];
            DrawPointStroke(new SKPoint(point.X, point.Y), Brush, DrawingColor, Opacity, 1);
        }
        else
        {
            var start = _pixelPerfectPreviewPoints[^2];
            var end = _pixelPerfectPreviewPoints[^1];
            DrawStroke(new SKPoint(start.X, start.Y), new SKPoint(end.X, end.Y), Brush, DrawingColor, 1);
        }

        PublishDisplaySnapshot();
    }

    private void RedrawPixelPerfectPreviewTail()
    {
        if (_swapBitmap == null)
            return;

        RenderIntoSwapBitmap(() =>
        {
            if (_pixelPerfectPreviewPoints.Count == 0)
                return;

            if (_pixelPerfectPreviewPoints.Count == 1)
            {
                var point = _pixelPerfectPreviewPoints[0];
                DrawPointStroke(new SKPoint(point.X, point.Y), Brush, DrawingColor, Opacity, 1);
                return;
            }

            var start = _pixelPerfectPreviewPoints[^2];
            var end = _pixelPerfectPreviewPoints[^1];
            DrawStroke(new SKPoint(start.X, start.Y), new SKPoint(end.X, end.Y), Brush, DrawingColor, 1);
        });

        PublishSwapBitmapDisplaySnapshot();
    }

    private void ClearPixelPerfectPreviewTail()
    {
        if (_swapBitmap == null)
            return;

        _swapBitmap.Clear();
        ClearSwapBitmapDisplaySnapshot();
    }

    private void RenderIntoSwapBitmap(Action render)
    {
        if (_swapBitmap == null)
            return;

        _swapBitmap.Clear();
        var previousUseSwapBitmap = UseSwapBitmap;
        UseSwapBitmap = true;
        try
        {
            render();
        }
        finally
        {
            UseSwapBitmap = previousUseSwapBitmap;
        }
    }

    private static bool ShouldSkipPixelPerfectPoint(SKPointI previous, SKPointI point, SKPointI next)
    {
        return (previous.X == point.X || previous.Y == point.Y)
               && (next.X == point.X || next.Y == point.Y)
               && previous.X != next.X
               && previous.Y != next.Y;
    }

    private List<SKPointI> PixelPerfect(List<SKPointI> path)
    {
        var cnt = path.Count;
        if (cnt <= 1)
        {
            return path;
        }

        var ret = new List<SKPointI>(cnt);
        var c = 0;

        while (c < cnt)
        {
            if (c > 0 && c + 1 < cnt && ShouldSkipPixelPerfectPoint(path[c - 1], path[c], path[c + 1]))
            {
                c += 1;
            }

            ret.Add(path[c]);

            c += 1;
        }

        return ret;
    }

    public void SetTarget(IDrawingTarget target)
    {
        var sameTarget = ReferenceEquals(target, DrawingTarget);
        DrawingTarget = target;
        DrawingTarget.FlushRequestedAction = FlushCurrentEditing;

        // Clamped, not asserted: a degenerate target must not produce 0x0 working/background bitmaps —
        // WorkingBitmap is non-nullable to every consumer, so refusing to allocate would only move the
        // failure. BeginDrawing is what actually refuses to start a stroke on such a target.
        var newSize = CanvasSize.Sanitize(DrawingTarget.GetSize());

        Debug.Assert(DrawingTarget.GetSize().GetSpace() > 0, "Size must not be 0");
        //if size changed, create new working bitmap
        if (Math.Abs(newSize.Width - Size.Width) > 0.01 || Math.Abs(newSize.Height - Size.Height) > 0.01)
        {
            _swapBitmap = new SKBitmap((int)newSize.Width, (int)newSize.Height, SKColorType.Rgba8888,
                SKAlphaType.Premul);
            _workingBitmap = _swapBitmap.Copy();
            _backgroundBitmap = _swapBitmap.Copy();

            Size = newSize;
        }
        else if (!sameTarget)
        {
            // Target changed (different sprite/frame/layer) but size matches — clear stale state so the new
            // target starts with clean working/background bitmaps.
            ClearWorkingBitmap();
        }
        // Same-target reattach (typical for tool switches) intentionally preserves working/background bitmaps:
        // an active selection's lifted-pixels state lives there and a tool transition shouldn't destroy it.

        if (State == DrawingLayerState.Drawing)
        {
            BeginDrawing();
        }
    }

    private void FlushCurrentEditing() => _selection.FlushCurrentEditing();

    public void SetPixel(int x, int y, SKColor color)
    {
        if (!InBounds(x, y) || WorkingBitmap.Width != (int)Size.Width || WorkingBitmap.Height != (int)Size.Height)
            return;

        WorkingBitmap.SetPixel(x, y, color);
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size.Width && y < Size.Height;

    public bool IsInBounds(SKPointI pos) => InBounds(pos.X, pos.Y);

    public void BeginDrawing()
    {
        // A zero-sized drawing target has no pixel buffer to snapshot, and DrawingStarted is what makes
        // DrawingService open an undo operation (target.GetData() -> BitmapNode.EnsureBitmap -> throw).
        // Pointer-down on such a sprite therefore used to fail on *every* stroke attempt rather than
        // degrade — appstat 3.10.0, `app_context: canvas=0x0`. The size itself is repaired upstream
        // (CanvasSize clamps at creation/mutation, SceneIntegrity on load); this keeps the stroke path
        // inert instead of fatal if one still slips through.
        if (DrawingTarget != null && CanvasSize.IsDegenerate(DrawingTarget.GetSize()))
        {
            Logger.Trace("BeginDrawing skipped: the drawing target has a degenerate canvas size "
                         + $"({DrawingTarget.GetSize().Width}x{DrawingTarget.GetSize().Height}).");
            return;
        }

        _strokePoints.Clear();
        _pixelPerfectPreviewPoints.Clear();
        ClearPixelPerfectPreviewTail();

        // Reset pressure to full at the start of every operation. The freehand path re-applies the live
        // value per stamp; shapes (and any non-freehand path) keep full pressure because they never do.
        if (_brush != null)
        {
            _brush.CurrentPressure = 1;
            // Dab spacing is stroke-local: the first dab of this stroke must land wherever the last one ended.
            _brush.BeginStroke();
        }

        DrawingStarted?.Invoke(this, EventArgs.Empty);

        if (DrawingTarget != null)
        {
            Opacity = DrawingTarget.GetOpacity();
            if (_backgroundBitmap != null)
                DrawingTarget.CopyBitmapTo(_backgroundBitmap);
            if (_backgroundBitmap != null)
                DrawingTarget.SetTargetBitmapSubstitute(() => _backgroundBitmap!);
        }
        // Pixel-perfect preview only needs double-buffer swapping when we must re-mask against the
        // background on every draw (LockTransparentPixels). In the normal path we keep a live bitmap
        // and publish immutable snapshots, which avoids O(n^2) full-stroke redraws during long pencil drags.
        UseSwapBitmap = IsPixelPerfectMode && LockTransparentPixels;

        // Soft brushes (Airbrush/Marker) draw as a streamlined, evenly-spaced stroke; pixel brushes keep the
        // legacy per-dab path. Pixel-perfect is its own pipeline, so it always opts out. Only Marker also uses
        // the even-opacity stroke buffer; Airbrush builds up per dab (composited opaque, like the legacy path).
        var style = _brush?.StrokeStyle ?? BrushStrokeStyle.Pixel;
        _useSmoothStroke = style != BrushStrokeStyle.Pixel
                           && _drawingMode == BrushDrawingMode.Draw
                           && !IsPixelPerfectMode;
        // The even-opacity composite is a property of the *brush*, not of who is driving it, so shapes
        // (ExternalDraw: line / rect / oval / triangle) opt in too — they stamp the same Marker dabs at full
        // strength into the stroke buffer, and without this the buffer was laid down opaque and the brush's
        // opacity silently vanished from every shape. Smoothing stays freehand-only: it reshapes the cursor
        // path, which a shape does not have.
        _strokeBufferOpacity = style == BrushStrokeStyle.Marker
                               && _drawingMode is BrushDrawingMode.Draw or BrushDrawingMode.ExternalDraw
                               && !IsPixelPerfectMode
            ? Math.Clamp(_brush!.Opacity, 0f, 1f)
            : null;
        _smoothStrokeStarted = false;
        _smoothSpacingLeftover = 0f;

        State = DrawingLayerState.Drawing;
    }

    public void FinishCurrentDrawing()
    {
        SwapWorkingBitmap();
        // Shape tools (line/rect/oval/triangle) call this every pointer-move during preview. Publishing
        // an immutable COW snapshot here hands the compositor stable pixels — without it the next
        // delta's bitmap writes race the compositor's flush, which the user sees as horizontal tear
        // bands across the preview. Same fix as the selection-transform path.
        PublishDisplaySnapshot();
    }

    private void ApplyWorkingBitmap()
    {
        // IsInitialized, not just DrawingTarget: SetTarget assigns the target first and allocates the
        // bitmaps after, and it skipped the allocation entirely for a degenerate (0x0) target. Releasing a
        // stroke on such a layer reached SKCanvas.DrawBitmap with a null bitmap and threw
        // ArgumentNullException from an ordinary pointer-up (appstat, 3.11.2, canvas=0x0). Nothing to
        // composite means nothing to apply.
        if (!IsInitialized)
            return;

        DrawingTarget!.Draw(drawingTargetCanvas =>
        {
            drawingTargetCanvas.Clear();
            drawingTargetCanvas.DrawBitmap(_backgroundBitmap, 0, 0);
            // For smooth soft-brush strokes the working bitmap is the full-strength stroke union; lay it down
            // once at the brush opacity. Otherwise alpha is 255 (the per-dab opacity is already baked in) — so
            // the legacy path stays pixel-for-pixel identical.
            var strokeAlpha = (byte)Math.Clamp((_strokeBufferOpacity ?? 1f) * 255f, 0, 255);
            var paint = new SKPaint()
            {
                Color = SKColor.Empty.WithAlpha(strokeAlpha),
                BlendMode = LockTransparentPixels && State == DrawingLayerState.Drawing ? SKBlendMode.SrcATop : SKBlendMode.SrcOver
            };
            drawingTargetCanvas.DrawBitmap(_workingBitmap, 0, 0, paint);
        });
    }

    public void ApplyDrawing()
    {
        if (State == DrawingLayerState.Drawing && DrawingTarget != null)
        {
            ApplyWorkingBitmap();

            DrawingTarget.ShowTargetBitmap();
            DrawingTarget.SetTargetBitmapSubstitute(null);

            ClearWorkingBitmap();
        }
    }

    public void FinishDrawing(bool cancel = false)
    {
        if (!cancel) ApplyWorkingBitmap();

        if (State == DrawingLayerState.Drawing && DrawingTarget != null)
        {
            DrawingTarget.ShowTargetBitmap();
            DrawingTarget.SetTargetBitmapSubstitute(null);
        }

        State = DrawingLayerState.Ready;
        ClearWorkingBitmap();
        _strokePoints.Clear();
        _pixelPerfectPreviewPoints.Clear();
        ClearPixelPerfectPreviewTail();
        Opacity = 1;
        UseSwapBitmap = false;

        // Reset smooth-stroke state so the next (possibly hard-brush) operation composites opaque again.
        // ApplyWorkingBitmap above has already consumed _strokeBufferOpacity for this stroke.
        _strokeBufferOpacity = null;
        _useSmoothStroke = false;
        _smoothStrokeStarted = false;

        OnDrawingApplied(!cancel);
    }

    public void CancelDrawing() => FinishDrawing(true);
    public void CancelSelect() => _selection.CancelSelect();

    private void UpdateBrushPreview(IPixelBrush? brush)
    {
        if (brush == null)
            return;

        var previewColor = _drawingMode == BrushDrawingMode.Erase ? SKColors.Gray : DrawingColor;

        // Rasterized outside the lock so the render thread is never blocked on an allocation.
        var surface = ((BasePixelBrush)brush)
            .GetPreviewSurface(previewColor.WithAlpha((byte)(brush.Opacity * 255)), brush.Size);

        SKSurface? previous;
        lock (_brushPreviewLock)
        {
            previous = _brushPreviewSurface;
            _brushPreviewSurface = surface;
        }

        // Safe here and only here: the render thread reaches the surface solely through the field, under the
        // same lock, so once the swap is published nothing can still be drawing the old one.
        previous?.Dispose();

        if (IsShowingBrush())
        {
            Refresh();
        }
    }

    private bool IsShowingBrush()
    {
        return ShowBrushPreview &&
               (State != DrawingLayerState.Drawing || _drawingMode == BrushDrawingMode.ExternalDraw);
    }

    /// <summary>
    /// Requests redrawing the drawing layer on the screen.
    /// </summary>
    private void Refresh()
    {
        LayerModified?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (IsShowingBrush())
        {
            RenderBrushPreview(canvas);
        }

        if (State == DrawingLayerState.Ready && !HasSelection)
        {
            return;
        }

        if (State == DrawingLayerState.Drawing && LockTransparentPixels)
        {
            if (_backgroundBitmap != null)
            {
                using var tmpBitmap = _backgroundBitmap.Copy();
                using var tmpCanvas = new SKCanvas(tmpBitmap);
                tmpCanvas.DrawBitmap(_workingBitmap, 0, 0, new SKPaint() { BlendMode = SKBlendMode.SrcIn });
                tmpCanvas.Flush();
                DrawStrokeBuffer(canvas, tmpBitmap);
            }
        }
        else
        {
            // Prefer the COW snapshot when one is published — see _workingBitmapDisplaySnapshot's note.
            // Falls back to the live bitmap when no snapshot is active (every path outside the selection
            // transform drag, where tearing isn't an issue). Lock covers the field read + DrawImage to
            // keep the UI thread from disposing the SKImage mid-call.
            lock (_snapshotLock)
            {
                if (_workingBitmapDisplaySnapshot != null)
                    DrawStrokeBuffer(canvas, _workingBitmapDisplaySnapshot);
                else if (_workingBitmap != null)
                    DrawStrokeBuffer(canvas, _workingBitmap);

                if (State == DrawingLayerState.Drawing && IsPixelPerfectMode && !UseSwapBitmap)
                {
                    if (_swapBitmapDisplaySnapshot != null)
                        canvas.DrawImage(_swapBitmapDisplaySnapshot, 0, 0);
                    else if (_swapBitmap != null)
                        canvas.DrawBitmap(_swapBitmap, 0, 0);
                }
            }
        }
    }

    // Composites the live stroke onto the screen. For smooth soft-brush strokes the working bitmap holds the
    // stroke unioned at full strength, so it's laid down once at the brush opacity (_strokeBufferOpacity);
    // otherwise it's drawn opaque (the per-dab opacity is already baked into the bitmap).
    private void DrawStrokeBuffer(SKCanvas canvas, SKBitmap bitmap)
    {
        using var paint = GetStrokeBufferPaint();
        canvas.DrawBitmap(bitmap, 0, 0, paint);
    }

    // Same composite for the immutable COW snapshot. Shapes publish one on every preview frame, so drawing
    // it raw here would have shown a Marker outline at full strength while the committed result honored the
    // brush opacity.
    private void DrawStrokeBuffer(SKCanvas canvas, SKImage image)
    {
        using var paint = GetStrokeBufferPaint();
        canvas.DrawImage(image, 0, 0, paint);
    }

    private SKPaint? GetStrokeBufferPaint() =>
        _strokeBufferOpacity is { } opacity
            ? new SKPaint { Color = SKColor.Empty.WithAlpha((byte)Math.Clamp(opacity * 255f, 0, 255)) }
            : null;

    private void RenderBrushPreview(SKCanvas canvas)
    {
        lock (_brushPreviewLock)
        {
            // The preview surface can be null when the brush couldn't produce a stamp bitmap or a surface
            // allocation failed (see BasePixelBrush.GetPreviewSurface); skip rather than NRE on DrawSurface.
            if (_brushPreviewSurface == null)
                return;

            canvas.DrawSurface(_brushPreviewSurface, PreviewPosition.X - Brush.PixelOffset.X, PreviewPosition.Y - Brush.PixelOffset.Y);

            // One preview stamp per symmetry image, so what the cursor shows is what a click would commit.
            _strokeRenderer.GetSymmetryImagesInto(PreviewPosition, Brush, _previewSymmetryImages);
            foreach (var image in _previewSymmetryImages)
                canvas.DrawSurface(_brushPreviewSurface, image.X - Brush.PixelOffset.X, image.Y - Brush.PixelOffset.Y);
        }
    }

    public void DrawWithBitmap(SKBitmap bitmap, SKRect destRect, SKBlendMode compositionMode, float opacity)
    {
        var paint = new SKPaint()
        {
            Color = SKColor.Empty.WithAlpha((byte)(opacity * 255)),
            BlendMode = compositionMode
        };

        // If DstOut is requested, the caller wants to erase from the DrawingTarget, so we want to use background
        // bitmap instead of working drawing bitmap.
        var workingBitmap = compositionMode == SKBlendMode.DstOut ? _backgroundBitmap : WorkingBitmap;

        if (workingBitmap != null)
        {
            using (var canvas = new SKCanvas(workingBitmap))
            {
                canvas.DrawBitmap(bitmap, destRect, paint);
                canvas.Flush();
            }

            workingBitmap.NotifyPixelsChanged();
        }
    }

    // --- Shape rasterization (the ExternalDraw tools: line / rect / oval / triangle) -----------------
    // Points arrive in LAYER-LOCAL space: the shape builders resolve the pointer with
    // GetPosition(drawingLayer) before calling in. The freehand path (DrawStroke/DrawPointStroke/
    // EraseStroke below) is the one that speaks world coordinates. See StrokeRenderer's class comment.
    //
    // Every one of these is a full outline pass: the shape tools re-run them from scratch on each pointer
    // move to refresh the preview, and once more to commit. So each pass restarts the brush's dab spacing —
    // otherwise the phase carried over from the previous pass, which made the dabs shuffle while dragging
    // and could swallow the opening dabs of a short shape drawn next to the previous one.
    public void DrawLine(SKPoint p0, SKPoint p1)
    {
        _brush?.BeginStroke();
        _strokeRenderer.DrawLine(p0, p1, Brush, DrawingColor, Opacity, 1);
    }

    public void DrawRect(SKPoint p0, SKPoint p1, bool fromCenter = false)
    {
        _brush?.BeginStroke();
        _strokeRenderer.DrawRect(p0, p1, Brush, DrawingColor, Opacity, 1);
    }

    public SKPoint ProjectAspectPoint(SKPoint p, SKPoint a, SKPoint? b)
    {
        var bb = new SKPoint(a.X + 1, a.Y - 1);
        if (b.HasValue)
        {
            bb = b.Value;
        }

        var atob = new SKPoint(bb.X - a.X, bb.Y - a.Y);
        var atop = new SKPoint(p.X - a.X, p.Y - a.Y);
        var len = atob.X * atob.X + atob.Y * atob.Y;
        var dot = atop.X * atob.X + atop.Y * atob.Y;
        var t = dot / len;
        //var t = Math.min(1, Math.max(0, dot / len));

        return new SKPoint(a.X + atob.X * t, a.Y + atob.Y * t);
    }

    public void DrawEllipse(SKPoint p0, SKPoint p1, bool fromCenter = false)
    {
        _brush?.BeginStroke();
        _strokeRenderer.DrawEllipse(p0, p1, Brush, DrawingColor, fromCenter);
    }

    public void DrawPointStroke(SKPoint p0, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        _strokeRenderer.DrawPointStroke(p0, brush, color, opacity, scale);
    }

    public void DrawStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        _strokeRenderer.DrawStroke(p0, p1, brush, color, opacity, scale);
    }

    public void EraseStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, float opacity)
    {
        _strokeRenderer.EraseStroke(p0, p1, brush, opacity);
    }

    public void FillRegion(SKPoint origin, SKColor fillColor, float tolerance = 0, SKBlendMode blendMode = SKBlendMode.SrcOver)
    {
        if (DrawingTarget == null)
            return;

        DrawingStarted?.Invoke(this, EventArgs.Empty);

        if (!_strokeRenderer.FillRegion(origin, fillColor, tolerance, blendMode))
            return;

        OnDrawingApplied(true);
    }

    public void SetDrawingLayerMode(BrushDrawingMode drawingMode)
    {
        _drawingMode = drawingMode;
        UpdateBrushPreview(_brush);
    }

    public void ClearTarget()
    {
        if (DrawingTarget == null)
            return;

        if (_selection.IsEditorVisible)
        {
            EraseSelection();
            OnDrawingApplied(true);
        }
        else
        {
            DrawingStarted?.Invoke(this, EventArgs.Empty);
            DrawingTarget.EraseBitmap();
            OnDrawingApplied(true);
        }

    }

    public void SelectAll() => _selection.SelectAll();
    public void SelectOpaquePixels(SKBitmap? maskSource) => _selection.SelectOpaquePixels(maskSource);
    public void FillSelection(SKColor color) => _selection.FillSelection(color);
    public void SetSelection(SpriteSelectionNode selectionLayer, SKBitmap? backgroundBitmap, bool contourOnly = false)
        => _selection.SetSelection(selectionLayer, backgroundBitmap, contourOnly);
    public void SetSelectionFromExternal(SKBitmap bitmap, in SKPoint position)
        => _selection.SetSelectionFromExternal(bitmap, position);
    public void BeginSelection(SKPoint pos, SelectionCombineMode combineMode = SelectionCombineMode.Replace)
        => _selection.BeginSelection(pos, combineMode);

    /// <summary>
    /// The selection the last marquee combined with (Shift/Ctrl), or null when it plainly replaced what was
    /// there. Read by <c>DrawingService</c> when it pushes the marquee's undo step.
    /// </summary>
    internal SelectionStateSnapshot? LastCombinedFromSelection => _selection.LastCombinedFromSelection;
    public void EraseSelection() => _selection.EraseSelection();
    public void ApplySelection(bool saveToUndo = false) => _selection.ApplySelection(saveToUndo);

    public void DrawBitmap(SKBitmap bitmap, SKPoint position)
    {
        if (DrawingTarget == null)
            return;

        DrawingStarted?.Invoke(this, EventArgs.Empty);

        DrawingTarget.Draw(canvas => canvas.DrawBitmap(bitmap, position));
        OnDrawingApplied(true);
    }

    public void InvalidateSelectionEditor() => _selection.InvalidateSelectionEditor();
    public void DeactivateSelectionEditor() => _selection.DeactivateSelectionEditor();
    public void FinishSelection() => _selection.FinishSelection();
    public void ActivateEditor() => _selection.ActivateEditor();
    public void ActivateEditor(bool contourOnly) => _selection.ActivateEditor(contourOnly);
    public void SetSelectionTransformMode(bool transformMode) => _selection.SetSelectionTransformMode(transformMode);
    public void SetFrameResizeMode(bool enabled) => _selection.SetFrameResizeMode(enabled);

    public void SetCustomPixelSelector(IPixelSelector pixelSelector) => _selection.SetCustomPixelSelector(pixelSelector);
    public void ClearCustomPixelSelector() => _selection.ClearCustomPixelSelector();


    public void CancelCurrentOperation()
    {
        switch (State)
        {
            case DrawingLayerState.Drawing:
            case DrawingLayerState.DrawingSelectionArea:
                CancelDrawing();
                break;
            case DrawingLayerState.Ready:
            case DrawingLayerState.Paste:
                CancelSelect();
                break;
        }
    }

    public void CancelActiveDrawing()
    {
        // A second finger / gesture suppression interrupts any pending touch selection before it promotes.
        _pointerInputRouter.CancelDeferredSelection();

        // Drop any in-flight marquee overlay so the dashed outline doesn't linger after a cancel.
        // Runs first so it covers both the DrawingSelectionArea path below and any state slip where the
        // overlay was attached but the layer state didn't track it.
        _selection.CancelMarqueeDrag();

        if (State is DrawingLayerState.Drawing or DrawingLayerState.DrawingSelectionArea)
        {
            CancelDrawing();
        }
    }

    public void AddSelectionPoint(SKPoint p) => _selection.AddSelectionPoint(p);
    public void SetSelectionRect(SKPoint startPos, SKPoint endPos) => _selection.SetSelectionRect(startPos, endPos);

    protected virtual void OnDrawingApplied(bool saveToUndo)
    {
        DrawingApplied?.Invoke(this, new DrawingAppliedEventArgs(saveToUndo));
    }

    public void FlipSelection(FlipMode mode) => _selection.FlipSelection(mode);
    public void RotateSelection(int angle) => _selection.RotateSelection(angle);

    public SKPoint SnapPointToAngleGrid(SKPoint p0, SKPoint p1, float angleStepDegrees = 15f)
    {
        var len = SKPoint.Distance(p0, p1);

        var dx = p1.X - p0.X;
        var dy = p1.Y - p0.Y;

        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

        var angleSnapped = Math.Round(angle / angleStepDegrees) * angleStepDegrees;
        var angleRad = (float)(angleSnapped * Math.PI / 180);
        var tp = new SKPoint((float)(p0.X + len * Math.Cos(angleRad)), (float)(p0.Y + len * Math.Sin(angleRad)));

        return ProjectAspectPoint(p1, p0, tp);
    }

    // ISelectionLayerHost — narrow seam for SelectionController. Public members of DrawingLayerNode
    // (DrawingTarget, Size, WorkingBitmap, UseSwapBitmap, State, Opacity, IsInBounds, SetPixel,
    // AspectSnapper, ActiveToolKeyProvider, GetGlobalTransform) already satisfy the interface
    // implicitly. The rest is wired explicitly so it doesn't leak into the public API.
    SKBitmap? ISelectionLayerHost.BackgroundBitmap
    {
        get => _backgroundBitmap;
        set => _backgroundBitmap = value;
    }

    void ISelectionLayerHost.ClearWorkingBuffers() => ClearWorkingBitmap();
    void ISelectionLayerHost.ClearWorkingAndSwapBitmaps()
    {
        _workingBitmap?.Clear();
        _swapBitmap?.Clear();
        ClearDisplaySnapshot();
    }
    void ISelectionLayerHost.SwapWorkingBitmap() => SwapWorkingBitmap();
    void ISelectionLayerHost.ApplyWorkingBitmap() => ApplyWorkingBitmap();
    void ISelectionLayerHost.RequestRefresh() => Refresh();
    void ISelectionLayerHost.RaiseDrawingApplied(bool saveToUndo) => OnDrawingApplied(saveToUndo);

    void ISelectionLayerHost.PromoteWorkingBitmapToDisplay() => PublishDisplaySnapshot();
    void ISelectionLayerHost.ClearDisplaySnapshot() => ClearDisplaySnapshot();

    BrushDrawingMode IPointerInputRouterHost.GetDrawingMode() => _drawingMode;
    bool IPointerInputRouterHost.IsTargetBitmapVisible => DrawingTarget!.IsTargetBitmapVisible();
    SKPoint IPointerInputRouterHost.StartPos
    {
        get => StartPos;
        set => StartPos = value;
    }
    SKPoint IPointerInputRouterHost.EndPos => EndPos;
    SKPointI IPointerInputRouterHost.StartPosI => StartPosI;
    SKPointI IPointerInputRouterHost.EndPosI => EndPosI;
    bool IPointerInputRouterHost.IsPointerOverSelection(SKPoint worldPos) => _selection.SelectionBoundsContains(worldPos);
    void IPointerInputRouterHost.ApplySelection() => ApplySelection();
    void IPointerInputRouterHost.Refresh() => Refresh();
    void IPointerInputRouterHost.CapturePointer() => CapturePointer();
    void IPointerInputRouterHost.DrawStroke(SKPoint pos) => DrawStroke(pos);
    void IPointerInputRouterHost.SetSelectionRect(SKPoint startPos, SKPoint endPos) => SetSelectionRect(startPos, endPos);
    void IPointerInputRouterHost.FinishReleasedDrawing() => FinishReleasedDrawing();
    void IPointerInputRouterHost.CancelMarquee()
    {
        // Drops the in-flight selector + dashed overlay and returns the layer to Ready. Used when a marquee
        // gesture turns out to be a plain click, which must leave nothing selected.
        _selection.CancelMarqueeDrag();
        State = DrawingLayerState.Ready;
        Refresh();
    }
}
