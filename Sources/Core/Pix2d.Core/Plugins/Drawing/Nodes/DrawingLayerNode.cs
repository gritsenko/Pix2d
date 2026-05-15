using System.Diagnostics;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Selection;
using Pix2d.InteractiveNodes;
using Pix2d.Plugins.Drawing.Brushes;
using Pix2d.Plugins.Drawing.Common;
using Pix2d.Plugins.Drawing.Common.Drawing;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Plugins.Drawing.PixelSelectors;
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

    private SKColor _drawingColor;
    private IPixelBrush? _brush;
    private SKSurface? _brushPreviewSurface;
    private readonly List<SKPointI> _strokePoints = new();

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

    public bool HasSelection => _selection.HasSelection;

    public SelectionPhase SelectionPhase => _selection.SelectionPhase;
    public bool MirrorX { get; set; }
    public bool MirrorY { get; set; }
    public static int MirrorYOffset { get; set; } = -1;
    public static int MirrorXOffset { get; set; } = -1;

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

        // Re-raise selection events through the node so external consumers (PixelSelectToolBase,
        // ExtractObjectTool, DrawingService, PixelTextTool) keep seeing the node as event sender.
        _selection.SelectionStarted     += (_, e) => SelectionStarted?.Invoke(this, e);
        _selection.SelectionRemoved     += (_, e) => SelectionRemoved?.Invoke(this, e);
        _selection.PixelsSelected       += (_, e) => PixelsSelected?.Invoke(this, e);
        _selection.PixelsBeforeSelected += (_, e) => PixelsBeforeSelected?.Invoke(this, e);
        _selection.SelectionTransformed += (_, e) => SelectionTransformed?.Invoke(this, e);
        _selection.MarqueeFinishedByUser += (_, e) => MarqueeFinishedByUser?.Invoke(this, e);
    }

    private void ClearWorkingBitmap()
    {
        _backgroundBitmap?.Clear();
        _workingBitmap?.Clear();
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

        // On touch screens move event might not capture last position, so we need to update it here
        // to prevent bugs with axis locked drawing.
        PreviewPosition = eventArgs.Pointer.GetPosition(this).ToSkPointI();

        if (_pointerInputRouter.ShouldIgnorePointerPressed(eventArgs))
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

        var prevPointerPosition = PreviewPosition;
        var currPointerPosition = eventArgs.Pointer.GetPosition(this).ToSkPointI();

        PreviewPosition = currPointerPosition;

        if (_drawingMode == BrushDrawingMode.ExternalDraw)
            return;

        base.OnPointerMoved(eventArgs);

        if (eventArgs.Pointer.IsPressed || eventArgs.Pointer.IsEraser)
        {
            _pointerInputRouter.HandlePointerMoved(eventArgs, prevPointerPosition, currPointerPosition);
        }

        Refresh();
    }

    private void FinishReleasedDrawing()
    {
        if (IsPixelPerfectMode && _strokePoints.Count > 1)
        {
            var ppf = PixelPerfect(_strokePoints);
            DrawStroke(ppf);
            SwapWorkingBitmap();
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

        if (_drawingMode == BrushDrawingMode.Draw)
        {
            if (IsPixelPerfectMode)
                DrawPixelPerfect(pos);
            else
                DrawStroke(LastPointerPosition, pos, Brush, DrawingColor, 1);
        }

        if (_drawingMode == BrushDrawingMode.Erase)
            EraseStroke(LastPointerPosition, pos, Brush, 1);

        LastPointerPosition = pos;
    }

    private void DrawPixelPerfect(SKPoint pos)
    {
        var intpos = pos.ToSkPointI();
        if (_strokePoints.Count > 0)
        {
            var lsp = _strokePoints.Last();
            if (intpos != lsp)
                _strokePoints.Add(intpos);
        }
        else
            _strokePoints.Add(intpos);

        DrawStroke(PixelPerfect(_strokePoints));
        SwapWorkingBitmap();
    }

    IEnumerable<SKPointI> PixelPerfect(List<SKPointI> path)
    {
        var cnt = path.Count();
        if (cnt <= 1)
        {
            return path;
        }

        var ret = new List<SKPointI>();
        var c = 0;

        while (c < cnt)
        {
            if (c > 0 && c + 1 < cnt
                      && (path[c - 1].X == path[c].X || path[c - 1].Y == path[c].Y)
                      && (path[c + 1].X == path[c].X || path[c + 1].Y == path[c].Y)
                      && path[c - 1].X != path[c + 1].X
                      && path[c - 1].Y != path[c + 1].Y)
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

        var newSize = DrawingTarget.GetSize();// new SKSizeI(bm.Width, bm.Height);

        Debug.Assert(newSize.GetSpace() > 0, "Size must not be 0");
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
        _strokePoints.Clear();

        DrawingStarted?.Invoke(this, EventArgs.Empty);

        if (DrawingTarget != null)
        {
            Opacity = DrawingTarget.GetOpacity();
            if (_backgroundBitmap != null)
                DrawingTarget.CopyBitmapTo(_backgroundBitmap);
            if (_backgroundBitmap != null)
                DrawingTarget.SetTargetBitmapSubstitute(() => _backgroundBitmap!);
        }
        UseSwapBitmap = IsPixelPerfectMode;

        State = DrawingLayerState.Drawing;
    }

    public void FinishCurrentDrawing()
    {
        SwapWorkingBitmap();
    }

    private void ApplyWorkingBitmap()
    {
        if (DrawingTarget == null)
            return;

        DrawingTarget.Draw(drawingTargetCanvas =>
        {
            drawingTargetCanvas.Clear();
            drawingTargetCanvas.DrawBitmap(_backgroundBitmap, 0, 0);
            var paint = new SKPaint()
            { BlendMode = LockTransparentPixels && State == DrawingLayerState.Drawing ? SKBlendMode.SrcATop : SKBlendMode.SrcOver };
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
        Opacity = 1;
        UseSwapBitmap = false;

        OnDrawingApplied(!cancel);
    }

    public void CancelDrawing() => FinishDrawing(true);
    public void CancelSelect() => _selection.CancelSelect();

    private void UpdateBrushPreview(IPixelBrush? brush)
    {
        if (brush == null)
            return;

        if (_drawingMode == BrushDrawingMode.Erase)
        {
            _brushPreviewSurface = ((BasePixelBrush)brush).GetPreviewSurface(SKColors.Gray.WithAlpha((byte)(brush.Opacity * 255)), brush.Size);
        }
        else
        {
            _brushPreviewSurface = ((BasePixelBrush)brush).GetPreviewSurface(DrawingColor.WithAlpha((byte)(brush.Opacity * 255)), brush.Size);
        }

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
                canvas.DrawBitmap(tmpBitmap, 0, 0);
            }
        }
        else
        {
            canvas.DrawBitmap(_workingBitmap, 0, 0);
        }
    }

    private void RenderBrushPreview(SKCanvas canvas)
    {
        canvas.DrawSurface(_brushPreviewSurface, PreviewPosition.X - Brush.PixelOffset.X, PreviewPosition.Y - Brush.PixelOffset.Y);

        if (MirrorY || MirrorX)
        {
            var mirrorPos = GetMirroredPoint(PreviewPosition, Brush.PixelOffset, Brush.Size);
            canvas.DrawSurface(_brushPreviewSurface, mirrorPos.X, mirrorPos.Y);
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

    public void DrawLine(SKPoint p0, SKPoint p1)
    {
        _strokeRenderer.DrawLine(p0, p1, Brush, DrawingColor, Opacity, 1);
    }

    public void DrawRect(SKPoint p0, SKPoint p1, bool fromCenter = false)
    {
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

    public SKPointI GetMirroredPoint(SKPointI p, SKPointI brushOffset = default, int brushSize = default)
    {
        return _strokeRenderer.GetMirroredPoint(p, brushOffset, brushSize);
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
    public void FillSelection(SKColor color) => _selection.FillSelection(color);
    public void SetSelection(SpriteSelectionNode selectionLayer, SKBitmap? backgroundBitmap, bool contourOnly = false)
        => _selection.SetSelection(selectionLayer, backgroundBitmap, contourOnly);
    public void SetSelectionFromExternal(SKBitmap bitmap, in SKPoint position)
        => _selection.SetSelectionFromExternal(bitmap, position);
    public void BeginSelection(SKPoint pos) => _selection.BeginSelection(pos);
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
    public void EnterTransformMode() => _selection.EnterTransformMode();
    public void SetSelectionTransformMode(bool transformMode) => _selection.SetSelectionTransformMode(transformMode);

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
    }
    void ISelectionLayerHost.SwapWorkingBitmap() => SwapWorkingBitmap();
    void ISelectionLayerHost.ApplyWorkingBitmap() => ApplyWorkingBitmap();
    void ISelectionLayerHost.RequestRefresh() => Refresh();
    void ISelectionLayerHost.RaiseDrawingApplied(bool saveToUndo) => OnDrawingApplied(saveToUndo);

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
}
