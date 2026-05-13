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

public class DrawingLayerNode : SKNode, IDrawingLayer, IPixelSelectionEditor
{

    public event EventHandler? DrawingStarted;
    public event EventHandler<DrawingAppliedEventArgs>? DrawingApplied;
    public event EventHandler? SelectionStarted;
    public event EventHandler? SelectionRemoved;

    public event EventHandler<PixelsBeforeSelectedEventArgs>? PixelsBeforeSelected;
    public event EventHandler<SelectionTransformedEventArgs>? SelectionTransformed;
    public event EventHandler? LayerModified;
    public event EventHandler? PixelsSelected;

    private IPixelSelector? _customPixelSelector;
    private IPixelSelector? _pixelSelector;

    private SKBitmap? _backgroundBitmap;
    private SKBitmap? _swapBitmap;
    private SKBitmap? _workingBitmap;

    public bool UseSwapBitmap { get; set; }

    /// <summary>
    /// The layer's bitmap currently displayed on the screen. All drawing and selection operations are rendered to
    /// this bitmap until they are applied to the <see cref="DrawingTarget"/>.
    /// </summary>
    public SKBitmap WorkingBitmap => (UseSwapBitmap ? _swapBitmap : _workingBitmap) ?? throw new InvalidOperationException("WorkingBitmap is not initialized");

    private SKPoint _lastPos;
    private BrushDrawingMode _drawingMode;
    private SpriteSelectionNode? _selectionLayer;
    private readonly FrameEditorNode _selectionEditor;

    // 2-pixel black/white dashes alternating along the selection outline (Photoshop "marching ants"
    // style). Visible on both light and dark backgrounds, unlike a single semi-transparent colour.
    private static SKColor GetSelectionDashColor(int x, int y)
        => (((x + y) >> 1) & 1) == 0 ? SKColors.White : SKColors.Black;
    private SKColor _drawingColor;
    private SKPointI _previewPos;
    private IPixelBrush? _brush;
    private SKSurface? _brushPreviewSurface;
    private readonly List<SKPointI> _strokePoints = new();
    private SelectionOperation? _currentSelectionOperation;

    // On touch, BeginSelection is deferred until the first pointer move past a small threshold so that
    // a pinch/pan gesture (which starts with a touch-down on the canvas) does not clear the current
    // selection. Distances are tracked in viewport pixels so the threshold stays physical regardless of zoom.
    private bool _deferredTouchSelectionStart;
    private SKPoint _deferredTouchStartViewportPos;
    private const float TouchDragThresholdViewportPixels = 12f;

    public bool HasSelectionChanges => _selectionEditor.IsChanged;

    /// <summary>
    /// True while the selected pixels are "lifted" out of the canvas — i.e. the source area is cleared
    /// from the background bitmap and the selection layer is composited on top via the working bitmap.
    /// In contour-edit mode (toggle off) the pixels are never lifted: dragging the marquee only reshapes
    /// what's selected without transforming the underlying image.
    /// </summary>
    private bool _pixelsLifted;

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
    public PixelSelectionMode SelectionMode { get; set; }
    public bool HasSelection => _selectionLayer != null;

    /// <summary>
    /// Derived from <see cref="HasSelection"/> + <see cref="_pixelsLifted"/>. Source of truth for tools deciding
    /// what selection-related actions are valid right now.
    /// </summary>
    public SelectionPhase SelectionPhase => !HasSelection
        ? SelectionPhase.None
        : _pixelsLifted
            ? SelectionPhase.Transforming
            : SelectionPhase.MarqueeReady;
    public bool MirrorX { get; set; }
    public bool MirrorY { get; set; }
    public static int MirrorYOffset { get; set; } = -1;
    public static int MirrorXOffset { get; set; } = -1;

    public bool LockTransparentPixels => DrawingTarget?.LockTransparentPixels ?? false;
    public bool ShowBrushPreview { get; set; }

    public SKSize SelectionSize { get; set; }

    private SKPointI StartPosI => StartPos.ToSkPointI();
    private SKPointI EndPosI => EndPos.ToSkPointI();
    public SKNode GetSelectionLayer()
    {
        return _selectionLayer ?? throw new InvalidOperationException("Selection layer is not initialized");
    }
    public IAspectSnapper? AspectSnapper { get; set; }

    /// <summary>
    /// Snapshot of the currently-active tool key. Used by selection-related operations so that
    /// <c>Undo</c>/<c>Redo</c> can restore the right tool and keep UI/drawing-layer state consistent
    /// (see <see cref="IToolAwareOperation"/>).
    /// </summary>
    public Func<string?>? ActiveToolKeyProvider { get; set; }

    public AxisLockMode AxisLockMode { get; set; }

    private bool IsInitialized => DrawingTarget != null && _backgroundBitmap != null && _workingBitmap != null;

    public SKBitmap GetSelectionBackground()
    {
        return _backgroundBitmap?.Copy() ?? throw new InvalidOperationException("BackgroundBitmap is not initialized");
    }

    public DrawingLayerNode()
    {
        IsInteractive = true;

        _selectionEditor = new FrameEditorNode();
        _selectionEditor.IsVisible = false;
        _selectionEditor.SelectionEditStarted += SelectionEditor_SelectionEditStarted;
        _selectionEditor.SelectionEdited += SelectionEditor_SelectionEdited;
        _selectionEditor.SelectionEditing += SelectionEditor_SelectionEditing;
        _selectionEditor.AspectSnapperProviderFunc = () => AspectSnapper!;
    }

    private void SelectionEditor_SelectionEditing(object? sender, EventArgs e)
    {
        if (_pixelsLifted)
            UpdateWorkingBitmapFromSelection();
        else
            Refresh();
    }

    /// <summary>
    /// Sets the content of working bitmap to the selection layer contents.
    /// </summary>
    private void UpdateWorkingBitmapFromSelection()
    {
        if (HasSelection)
        {
            using var canvas = new SKCanvas(WorkingBitmap);
            
            var target = ((SKNode)DrawingTarget!).Position;

            var vp = new ViewPort((int)Size.Width, (int)Size.Height);
            vp.SetPan(target.X, target.Y);

            SKNodeRenderer.Render(_selectionLayer!, new RenderContext(canvas, vp));
            canvas.Flush();
            WorkingBitmap.NotifyPixelsChanged();
            SwapWorkingBitmap();
        }
    }

    private void ClearWorkingBitmap()
    {
        _backgroundBitmap?.Clear();
        _workingBitmap?.Clear();
    }

    private SelectionOperation GetCurrentSelectionOperation()
    {
        if (_currentSelectionOperation == null)
        {
            return new SelectionOperation(this, ActiveToolKeyProvider?.Invoke());
        }
        else
        {
            return new SelectionOperation(_currentSelectionOperation);
        }
    }

    private void SelectionEditor_SelectionEdited(object? sender, EventArgs e)
    {
        _currentSelectionOperation!.SetFinalState(ActiveToolKeyProvider?.Invoke());
        if (_pixelsLifted)
            UpdateWorkingBitmapFromSelection();
        else
            Refresh();
        OnSelectionTransformed(_currentSelectionOperation);
    }

    private void SelectionEditor_SelectionEditStarted(object? sender, EventArgs e)
    {
        _currentSelectionOperation = GetCurrentSelectionOperation();
        if (_pixelsLifted)
            UpdateWorkingBitmapFromSelection();
    }

    public override bool ContainsPoint(SKPoint worldPos)
    {
        return true;
    }

    public override void OnPanModeChanged(bool isPanModeEnabled)
    {
        if (_drawingMode == BrushDrawingMode.Draw)
            CancelDrawing();

        if (isPanModeEnabled && _deferredTouchSelectionStart)
        {
            // Pinch/pan gesture started before the user actually began a new selection:
            // drop the deferred start so the existing selection is preserved.
            _deferredTouchSelectionStart = false;
        }

        //if (_drawingMode == BrushDrawingMode.Select)
        //    CancelSelect();
    }

    public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
        if (!IsInitialized) return;

        // On touch screens move event might not capture last position, so we need to update it here
        // to prevent bugs with axis locked drawing.
        _previewPos = eventArgs.Pointer.GetPosition(this).ToSkPointI();

        if (_drawingMode == BrushDrawingMode.MoveSelection)
            return;

        if (State == DrawingLayerState.Paste)
        {
            ApplySelection();
        }

        if (_drawingMode == BrushDrawingMode.ExternalDraw || _drawingMode == BrushDrawingMode.Fill || _drawingMode == BrushDrawingMode.FillErase || (eventArgs.KeyModifiers & KeyModifier.Alt) != 0)
            return;

        base.OnPointerPressed(eventArgs, clickCount);

        _lastPos = eventArgs.Pointer.WorldPosition.ToSkPointI();
        if (_drawingMode == BrushDrawingMode.Select)
        {
            if (eventArgs.Pointer.IsTouch)
            {
                _deferredTouchSelectionStart = true;
                _deferredTouchStartViewportPos = eventArgs.Pointer.ViewportPosition;
                return;
            }

            CapturePointer();
            BeginSelection(StartPosI);
            AddSelectionPoint(StartPosI);
        }
        else
        {
            CapturePointer();
            if (DrawingTarget!.IsTargetBitmapVisible())
                BeginDrawing();

            if (State == DrawingLayerState.Drawing)
            {
                DrawStroke(_lastPos);
                Refresh();
            }
        }
    }

    public override void OnPointerReleased(PointerActionEventArgs eventArgs)
    {
        if (!IsInitialized) return;

        ReleasePointerCapture();

        if (_deferredTouchSelectionStart)
        {
            // Touch tap (release below drag threshold) — same semantics as a mouse click in an empty area:
            // drop the current selection. Pinch/pan never reaches release because OnPanModeChanged cancels
            // the deferred start on the first finger when the second finger arrives.
            _deferredTouchSelectionStart = false;
            if (HasSelection)
            {
                ApplySelection();
                Refresh();
            }
            base.OnPointerReleased(eventArgs);
            return;
        }

        try
        {
            if (_selectionEditor.IsVisible &&
                _selectionEditor.SelectionBounds.Contains(eventArgs.Pointer.WorldPosition))
            {
                return;
            }


            AxisLockMode = AxisLockMode.None;

            if (_drawingMode == BrushDrawingMode.ExternalDraw || _drawingMode == BrushDrawingMode.MoveSelection)
            {
                return;
            }

            if (State == DrawingLayerState.DrawingSelectionArea && _drawingMode == BrushDrawingMode.Select)
            {
                AddSelectionPoint(StartPosI);
                FinishSelection();
            }
            else if (_drawingMode == BrushDrawingMode.Fill)
            {
                FillRegion(EndPos, DrawingColor);
            }
            else if (_drawingMode == BrushDrawingMode.FillErase)
            {
                FillRegion(EndPos, SKColors.White, blendMode: SKBlendMode.DstOut);
            }
            else
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
                        ErasePoint(Brush, EndPosI, 1);
                    }
                }

                if (State != DrawingLayerState.Ready)
                {
                    FinishDrawing();
                }
            }
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

        var prevPointerPosition = _previewPos;
        var currPointerPosition = eventArgs.Pointer.GetPosition(this).ToSkPointI();

        _previewPos = currPointerPosition;

        if (_drawingMode == BrushDrawingMode.ExternalDraw)
            return;

        base.OnPointerMoved(eventArgs);

        if (eventArgs.Pointer.IsPressed || eventArgs.Pointer.IsEraser)
        {
            if (_deferredTouchSelectionStart)
            {
                var dx = eventArgs.Pointer.ViewportPosition.X - _deferredTouchStartViewportPos.X;
                var dy = eventArgs.Pointer.ViewportPosition.Y - _deferredTouchStartViewportPos.Y;
                if (dx * dx + dy * dy < TouchDragThresholdViewportPixels * TouchDragThresholdViewportPixels)
                {
                    // Still within the dead zone — keep waiting, user might be about to pinch/pan.
                    return;
                }

                _deferredTouchSelectionStart = false;
                CapturePointer();
                BeginSelection(StartPosI);
                AddSelectionPoint(StartPosI);
                _lastPos = StartPos.ToSkPointI();
            }

            if (State == DrawingLayerState.Drawing)
            {
                var strokeEndPos = EndPosI;
                if (AspectSnapper?.IsAspectLocked == true)
                {
                    if (AxisLockMode == AxisLockMode.None && currPointerPosition != prevPointerPosition)
                    {
                        var delta = _previewPos - prevPointerPosition;
                        AxisLockMode = Math.Abs(delta.X) > Math.Abs(delta.Y) ? AxisLockMode.Horizontal : AxisLockMode.Vertical;
                        StartPos = prevPointerPosition;
                    }

                    if (AxisLockMode == AxisLockMode.Horizontal)
                    {
                        strokeEndPos = new SKPointI(EndPosI.X, StartPosI.Y);
                    }
                    else if (AxisLockMode == AxisLockMode.Vertical)
                    {
                        strokeEndPos = new SKPointI(StartPosI.X, EndPosI.Y);
                    }
                }

                DrawStroke(strokeEndPos);
            }
            else if (State == DrawingLayerState.DrawingSelectionArea)
            {
                switch (SelectionMode)
                {
                    case PixelSelectionMode.Rectangle:
                        SetSelectionRect(StartPosI, EndPosI);
                        break;
                    case PixelSelectionMode.Freeform:
                        AddSelectionPoint(EndPos);
                        break;
                    case PixelSelectionMode.SameColor:
                        //nothing to-do in while pointer moving
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        Refresh();
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
                ErasePoint(Brush, pp[0], 1);
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
                DrawStroke(_lastPos, pos, Brush, DrawingColor, 1);
        }

        if (_drawingMode == BrushDrawingMode.Erase)
            EraseStroke(_lastPos, pos, Brush, 1);

        _lastPos = pos;
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

    private void FlushCurrentEditing()
    {
        if (_selectionEditor == null || !_selectionEditor.IsVisible) return;
        if (_selectionEditor.IsChanged)
            ApplySelection();
        else
            CancelSelect();
    }

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
    public void CancelSelect()
    {
        DeactivateSelectionEditor();
    }

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
        canvas.DrawSurface(_brushPreviewSurface, _previewPos.X - Brush.PixelOffset.X, _previewPos.Y - Brush.PixelOffset.Y);

        if (MirrorY || MirrorX)
        {
            var mirrorPos = GetMirroredPoint(_previewPos, Brush.PixelOffset, Brush.Size);
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
        DrawStroke(p0, p1, Brush, DrawingColor, Opacity, 1);
    }

    public void DrawRect(SKPoint p0, SKPoint p1, bool fromCenter = false)
    {
        var p00 = p0;
        var p01 = new SKPoint(p1.X, p0.Y);
        var p02 = p1;
        var p03 = new SKPoint(p0.X, p1.Y);

        DrawStroke(p00, p01, Brush, DrawingColor, Opacity, 1);
        DrawStroke(p01, p02, Brush, DrawingColor, Opacity, 1);
        DrawStroke(p02, p03, Brush, DrawingColor, Opacity, 1);
        DrawStroke(p00, p03, Brush, DrawingColor, Opacity, 1);
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
        var xc = p0.X;
        var yc = p0.Y;

        var w = (int)Math.Abs((p1.X - p0.X));
        var h = (int)Math.Abs((p1.Y - p0.Y));

        int dx = 0, dy = 0;

        var width = w;
        var height = h;

        if (!fromCenter)
        {
            xc = (int)((p0.X + p1.X) * 0.5);
            yc = (int)((p0.Y + p1.Y) * 0.5);

            dx = w % 2 > 0 ? 1 : 0;
            dy = h % 2 > 0 ? 1 : 0;

            width = (int)w / 2;
            height = (int)h / 2;
        }

        if (w < 3 || h < 3)
        {
            DrawRect(p0, p1, fromCenter);
            return;
        }

        int a2 = width * width;
        int b2 = height * height;
        int fa2 = 4 * a2, fb2 = 4 * b2;
        int x, y, sigma;

        Action<double, double> plot = (xp, yp) => DrawPoint(Brush, new SKPointI((int)xp, (int)yp), DrawingColor, Brush.Size, false);
        Action<double, double> plot4 = (xp, yp) =>
        {
            plot(xc + xp + dx, yc + yp + dy);
            plot(xc - xp, yc + yp + dy);
            plot(xc + xp + dx, yc - yp);
            plot(xc - xp, yc - yp);
        };
        /* first half */
        for (x = 0, y = height, sigma = 2 * b2 + a2 * (1 - 2 * height); b2 * x <= a2 * y; x++)
        {
            plot4(x, y);
            if (sigma >= 0)
            {
                sigma += fa2 * (1 - y);
                y--;
            }
            sigma += b2 * ((4 * x) + 6);
        }

        /* second half */
        for (x = width, y = 0, sigma = 2 * a2 + b2 * (1 - 2 * width); a2 * y <= b2 * x; y++)
        {
            plot4(x, y);
            if (sigma >= 0)
            {
                sigma += fb2 * (1 - x);
                x--;
            }
            sigma += a2 * ((4 * y) + 6);
        }
    }

    public void DrawPointStroke(SKPoint p0, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        GetGlobalTransform().TryInvert(out var invertedTransform);
        var pivot = SKPoint.Empty;
        var p00 = invertedTransform.MapPoint(p0 + pivot).ToSkPointI();
        DrawPoint(brush, p00, color, (int)scale, true);
    }

    public void DrawStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, SKColor color, float opacity, float scale = 1)
    {
        if (float.IsNaN(p1.Length) || float.IsNaN(p0.Length))
            return;

        GetGlobalTransform().TryInvert(out var invertedTransform);
        var p00 = invertedTransform.MapPoint(p0).ToSkPointI();
        var p01 = invertedTransform.MapPoint(p1).ToSkPointI();

        Algorithms.LineNotSwapped(p00.X, p00.Y, p01.X, p01.Y, (x, y) =>
        {
            var p = new SKPointI(x, y);
            if (!IsInBounds(p))
                return false;

            DrawPoint(brush, p, color, (int)scale);
            return true;
        });
    }

    private void DrawPoint(IPixelBrush brush, SKPointI p, SKColor color, int scale, bool ignoreSpacing = false)
    {
        var isDrawn = brush.Draw(this, p, color, scale, ignoreSpacing);

        if (isDrawn && (MirrorX || MirrorY))
        {
            var ox = MirrorX ? brush.PixelOffset.X * 2 : 0;
            var oy = MirrorY ? brush.PixelOffset.Y * 2 : 0;
            brush.Draw(this, GetMirroredPoint(p, new SKPointI(ox, oy), Brush.Size), color, scale, true);
        }
    }

    public SKPointI GetMirroredPoint(SKPointI p, SKPointI brushOffset = default, int brushSize = default)
    {
        var xx = p.X;
        if (MirrorX)
        {
            xx = (int)(Size.Width - p.X) - brushSize;
        }

        var yy = p.Y;
        if (MirrorY)
        {
            yy = (int)(Size.Height - p.Y) - brushSize;
        }

        if (brushOffset != default)
        {
            xx += MirrorX ? brushOffset.X : -brushOffset.X;
            yy += MirrorY ? brushOffset.Y : -brushOffset.Y;
        }

        return new SKPointI(xx, yy);
    }

    private void ErasePoint(IPixelBrush brush, SKPointI p, int scale)
    {
        var isDrawn = brush.Erase(this, p, scale, true);

        if (isDrawn && (MirrorX || MirrorY))
        {
            var ox = MirrorX ? brush.PixelOffset.X * 2 : 0;
            var oy = MirrorY ? brush.PixelOffset.Y * 2 : 0;
            brush.Erase(this, GetMirroredPoint(p, new SKPointI(ox, oy), Brush.Size), scale, true);
        }
    }

    public void EraseStroke(SKPoint p0, SKPoint p1, IPixelBrush brush, float opacity)
    {
        var Pivot = SKPoint.Empty;
        GetGlobalTransform().TryInvert(out var invertedTransform);

        var p00 = invertedTransform.MapPoint(p0 + Pivot);
        var p01 = invertedTransform.MapPoint(p1 + Pivot);

        Algorithms.LineNotSwapped((int)p00.X, (int)p00.Y, (int)p01.X, (int)p01.Y, (x, y) =>
        {
            var p = new SKPointI(x, y);
            if (!IsInBounds(p))
                return false;

            ErasePoint(brush, p, 1);
            //brush.Erase(this, p, 1, true);
            //layer.DrawPoint(x, y, brush, color, pressure, false);

            return true;
        });
    }

    public void FillRegion(SKPoint origin, SKColor fillColor, float tolerance = 0, SKBlendMode blendMode = SKBlendMode.SrcOver)
    {
        if (DrawingTarget == null)
            return;

        DrawingStarted?.Invoke(this, EventArgs.Empty);

        var Pivot = SKPoint.Empty;

        //get local transform of pointer relative working bitmap
        GetGlobalTransform().TryInvert(out var invertedTransform);
        var origin0 = invertedTransform.MapPoint(origin + Pivot);

        if (!InBounds((int)origin0.X, (int)origin0.Y))
            return;

        DrawingTarget.ModifyBitmap(bitmap => FloodFillBitmap(origin0, fillColor, bitmap, tolerance, blendMode));
        OnDrawingApplied(true);
    }

    private void FloodFillBitmap(SKPoint origin0, SKColor fillColor, SKBitmap bitmap, float tolerance, SKBlendMode blendMode)
    {
        var floodFiller = new FloodFiller(bitmap.Pixels, new SKSizeI(bitmap.Width, bitmap.Height));
        floodFiller.FloodFill(new SKPointI((int)origin0.X, (int)origin0.Y), fillColor);

        var data = floodFiller.GetPixelBytes();
        WorkingBitmap.CopyPixelsToBitmap(data);

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { BlendMode = blendMode };
        canvas.DrawBitmap(WorkingBitmap, 0, 0, paint);
    }

    public void SetDrawingLayerMode(BrushDrawingMode drawingMode)
    {
        _drawingMode = drawingMode;
        if (_drawingMode == BrushDrawingMode.MoveSelection)
        {
            _selectionEditor.AllowResize = false;
        }
        else if (_drawingMode == BrushDrawingMode.Select)
        {
            _selectionEditor.AllowResize = true;
        }

        UpdateBrushPreview(_brush);
    }

    public void ClearTarget()
    {
        if (DrawingTarget == null)
            return;

        if (_selectionEditor.IsVisible)
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

    public void SelectAll()
    {
        ApplySelection();
        OnSelectionStarted();
        _pixelSelector = new AllPixelSelector();
        FinishSelection();
    }

    public void FillSelection(SKColor color)
    {
        if (!HasSelection || _selectionLayer == null) return;

        using (var canvas = new SKCanvas(WorkingBitmap))
        {
            canvas.Clear();

            var blendMode = SKBlendMode.SrcOver;
            if (LockTransparentPixels && _selectionLayer.Bitmap != null)
            {
                canvas.DrawBitmap(_selectionLayer.Bitmap, _selectionLayer.Position);
                blendMode = SKBlendMode.SrcIn;
            }

            canvas.Save();

            var rot = SKMatrix.CreateRotationDegrees(_selectionLayer.Rotation, _selectionLayer.PivotPosition.X, _selectionLayer.PivotPosition.Y);
            var trans = SKMatrix.CreateTranslation(_selectionLayer.Position.X - _selectionLayer.PivotPosition.X, _selectionLayer.Position.Y - _selectionLayer.PivotPosition.Y);
            var scaleX = _selectionLayer.Size.Width / (_selectionLayer.Bitmap?.Width ?? 1);
            var scaleY = _selectionLayer.Size.Height / (_selectionLayer.Bitmap?.Height ?? 1);
            var scale = SKMatrix.CreateScale(scaleX, scaleY);

            if (_selectionLayer.SelectionPath != null)
            {
                // TODO: There is a bug here. If selection is scaled by not right bottom handle, or if rotation and
                // scale applied, position of the fill will be wrong.
                var localTransform = SKMatrix.Identity;
                SKMatrix.Concat(ref localTransform, localTransform, trans);
                SKMatrix.Concat(ref localTransform, localTransform, rot);
                SKMatrix.Concat(ref localTransform, localTransform, scale);
                SKMatrix.Concat(ref localTransform, localTransform, trans.Invert());

                canvas.SetMatrix(localTransform);
                canvas.DrawPath(_selectionLayer.SelectionPath, new SKPaint { IsStroke = false, Color = color, BlendMode = blendMode });
            }
            else
            {
                var localTransform = trans;
                SKMatrix.Concat(ref localTransform, localTransform, rot);
                SKMatrix.Concat(ref localTransform, localTransform, scale);

                canvas.SetMatrix(localTransform);

                if (_selectionLayer.Bitmap != null)
                {
                    _selectionLayer.Bitmap.Erase(color);
                    canvas.DrawBitmap(_selectionLayer.Bitmap, SKPoint.Empty, new SKPaint() { BlendMode = blendMode });
                }
            }
            canvas.Restore();
        }

        if (WorkingBitmap == _swapBitmap)
        {
            SwapWorkingBitmap();
        }

        _selectionEditor.SetIsChanged();
        ApplySelection(true);
    }

    public void SetSelection(SpriteSelectionNode selectionLayer, SKBitmap? backgroundBitmap, bool contourOnly = false)
    {
        if (DrawingTarget == null)
            return;

        OnSelectionStarted();
        ClearWorkingBitmap();

        _selectionLayer = selectionLayer;


        if (backgroundBitmap == null)
        {
            if (_backgroundBitmap != null)
                DrawingTarget.CopyBitmapTo(_backgroundBitmap);
            State = DrawingLayerState.Paste;
        }
        else
        {
            State = DrawingLayerState.Ready;
            _backgroundBitmap = backgroundBitmap.Copy();
        }

        Opacity = DrawingTarget.GetOpacity();

        // In transform mode the lifted pixels need to be rendered onto the working bitmap; in contour mode
        // they stay on the target and the working bitmap stays empty (marching ants only).
        if (!contourOnly)
            UpdateWorkingBitmapFromSelection();

        ActivateEditor(contourOnly: contourOnly);

        _selectionEditor.SetIsChanged();
        OnPixelsSelected();
    }

    public void SetSelectionFromExternal(SKBitmap bitmap, in SKPoint position)
    {
        if (DrawingTarget == null)
            return;

        var layer = new SpriteSelectionNode
        {
            Bitmap = bitmap,
            Opacity = 1,
            Position = position + ((SKNode)DrawingTarget).Position,
            Size = bitmap.Info.Size,
        };

        SetSelection(layer, null);
    }

    public void BeginSelection(SKPoint pos)
    {
        ApplySelection();
        OnSelectionStarted();
        State = DrawingLayerState.DrawingSelectionArea;
        ClearWorkingBitmap();
        WorkingBitmap.NotifyPixelsChanged();

        if (DrawingTarget != null && SelectionMode == PixelSelectionMode.SameColor)
        {
            var size = DrawingTarget.GetSize();
            var bitmap = new SKBitmap(new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Rgba8888));
            DrawingTarget.CopyBitmapTo(bitmap);
            _pixelSelector = new SameColorSelector(bitmap);
        }
        else
        {
            _pixelSelector = _customPixelSelector ?? new PixelSelector();
        }

        UseSwapBitmap = SelectionMode == PixelSelectionMode.Rectangle;
        _pixelSelector.BeginSelection(new SKPointI((int)pos.X, (int)pos.Y));
    }

    public void EraseSelection()
    {
        _pixelSelector = null;
        if (_selectionLayer == null)
        {
            return;
        }

        if (_workingBitmap != null)
            _workingBitmap.Clear();
        ApplyWorkingBitmap();
        DeactivateSelectionEditor();
    }

    public void ApplySelection(bool saveToUndo = false)
    {
        _pixelSelector = null;
        if (_selectionLayer == null)
        {
            return;
        }

        // In contour-edit mode pixels aren't lifted, so the working bitmap doesn't carry the
        // transformed state — applying must not stamp it onto the target (that would just erase
        // the marquee's source area). The contour-mode marquee changes only reshape the selection
        // region, not the image.
        if (_pixelsLifted && _selectionEditor.IsChanged)
        {
            ApplyWorkingBitmap();
            OnDrawingApplied(saveToUndo);
        }

        DeactivateSelectionEditor();
    }

    public void DrawBitmap(SKBitmap bitmap, SKPoint position)
    {
        if (DrawingTarget == null)
            return;

        DrawingStarted?.Invoke(this, EventArgs.Empty);

        DrawingTarget.Draw(canvas => canvas.DrawBitmap(bitmap, position));
        OnDrawingApplied(true);
    }

    public void InvalidateSelectionEditor()
    {
        if (_selectionLayer != null)
            _selectionEditor.SetSelection(new NodesSelection(new[] { _selectionLayer }, () => { }) { GenerateOperations = false });
    }

    public void DeactivateSelectionEditor()
    {
        _selectionEditor.Hide();

        _selectionLayer = null;
        _currentSelectionOperation = null;

        _backgroundBitmap?.Clear();
        _workingBitmap?.Clear();

        UseSwapBitmap = false;
        Opacity = 1;

        DrawingTarget?.SetTargetBitmapSubstitute(null);
        DrawingTarget?.ShowTargetBitmap();

        OnSelectionRemoved();
        OnDrawingApplied(false);

        State = DrawingLayerState.Ready;
    }

    /// <summary>
    /// Activates selection editor using currently drawn selection area. If nothing is selected (1 pixel is in
    /// the selected area), editor is not activated.
    /// </summary>
    public void FinishSelection()
    {
        if (_pixelSelector == null || DrawingTarget == null)
            return;

        State = DrawingLayerState.Ready;

        var selector = _pixelSelector;
        selector.FinishSelection(SelectionMode != PixelSelectionMode.Rectangle);

        var size = DrawingTarget.GetSize();
        var tmpBitmap = new SKBitmap(new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Rgba8888));
        DrawingTarget.CopyBitmapTo(tmpBitmap);
        var selectionBitmap = selector.GetSelectionBitmap(tmpBitmap);

        selector.ClearSelectionFromBitmap(ref tmpBitmap);

        if (selectionBitmap.Pixels.Length > 1)
        {
            OnPixelsBeforeSelected(selectionBitmap);

            _selectionLayer = new SpriteSelectionNode
            {
                Bitmap = selectionBitmap,
                SelectionPath = selector.GetSelectionPath(),
                Opacity = 1,
                Position = selector.Offset + ((SKNode)DrawingTarget).Position,
            };

            Opacity = DrawingTarget.GetOpacity();
            _backgroundBitmap = tmpBitmap;

            // Selection tools (Rect / Lasso / Color) always finish in contour-only mode — they never lift
            // pixels. Auto-enter into transform mode (if enabled) is handled by DrawingService switching to
            // PixelTransformTool, which is the single owner of the "pixels lifted" state.
            ActivateEditor(contourOnly: true);
            OnPixelsSelected();
        }
    }

    /// <summary>
    /// Activates the selection editor. Use the overload taking <c>contourOnly</c> explicitly to make the mode
    /// choice obvious at the call site.
    /// </summary>
    public void ActivateEditor()
    {
        // Defaults to transform mode for backwards-compat with callers that conceptually want the "lifted"
        // experience (e.g. paste / undo of selection ops). Selection tools should call the overload with
        // contourOnly: true explicitly.
        ActivateEditor(contourOnly: false);
    }

    public void ActivateEditor(bool contourOnly)
    {
        if (DrawingTarget is SKNode target && _selectionLayer != null)
        {
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(target.Parent ?? target);
            adornerLayer.Add(_selectionEditor);

            var selection = new NodesSelection(new[] { _selectionLayer }, () => { }) { GenerateOperations = false };

            _selectionEditor.SetSelection(selection, _selectionLayer.SelectionPath);
            _selectionEditor.ContourOnly = contourOnly;
            _selectionEditor.IsVisible = true;

            UseSwapBitmap = true;

            if (contourOnly)
            {
                // Marquee-only mode: don't lift pixels. Working bitmap stays empty so the underlying
                // canvas is shown unchanged, and dragging the thumbs only reshapes the selection.
                _pixelsLifted = false;
                _workingBitmap?.Clear();
                _swapBitmap?.Clear();
                DrawingTarget.SetTargetBitmapSubstitute(null);
            }
            else
            {
                _pixelsLifted = true;
                UpdateWorkingBitmapFromSelection();
                if (_backgroundBitmap != null)
                {
                    DrawingTarget.SetTargetBitmapSubstitute(() => _backgroundBitmap!);
                }
            }
        }
    }

    /// <summary>
    /// Switches an active contour-only selection into the full transform mode (move/resize/rotate handles).
    /// Invoked from the Clipboard Actions panel "Transform" button. No-op if there is no selection.
    /// </summary>
    public void EnterTransformMode()
    {
        if (_selectionLayer == null) return;

        if (!_selectionEditor.IsVisible)
        {
            ActivateEditor(contourOnly: false);
        }
        else
        {
            _selectionEditor.ContourOnly = false;
        }

        // Toggling thumb visibility doesn't dirty the scene tree on its own, so force a redraw here.
        Refresh();
    }

    public void SetSelectionTransformMode(bool transformMode)
    {
        if (_selectionLayer == null || !_selectionEditor.IsVisible) return;

        var contourOnly = !transformMode;
        if (_selectionEditor.ContourOnly == contourOnly) return;

        if (transformMode)
            LiftSelectionFromCanvas();
        else
            CommitWorkingBitmapToCanvas();

        _selectionEditor.ContourOnly = contourOnly;
        Refresh();
    }

    private void LiftSelectionFromCanvas()
    {
        if (_pixelsLifted || DrawingTarget == null || _selectionLayer == null) return;

        var size = DrawingTarget.GetSize();
        var snapshot = new SKBitmap(new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Rgba8888));
        DrawingTarget.CopyBitmapTo(snapshot);

        var bounds = _selectionLayer.GetBoundingBox();
        var origin = ((SKNode)DrawingTarget).Position;
        int x = (int)Math.Round(bounds.Left - origin.X);
        int y = (int)Math.Round(bounds.Top - origin.Y);
        int w = (int)Math.Round(bounds.Width);
        int h = (int)Math.Round(bounds.Height);
        if (w <= 0 || h <= 0) return;

        var selBitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888));
        using (var c = new SKCanvas(selBitmap))
        {
            c.DrawBitmap(snapshot, new SKRect(x, y, x + w, y + h), new SKRect(0, 0, w, h));
        }

        using (var c = new SKCanvas(snapshot))
        using (var clear = new SKPaint { BlendMode = SKBlendMode.Clear })
        {
            c.DrawRect(new SKRect(x, y, x + w, y + h), clear);
        }

        _selectionLayer.Bitmap = selBitmap;
        _selectionLayer.Size = new SKSize(w, h);
        _selectionLayer.Position = new SKPoint(bounds.Left, bounds.Top);
        _selectionLayer.SelectionPath = null;
        _selectionLayer.Rotation = 0;

        _backgroundBitmap = snapshot;
        DrawingTarget.SetTargetBitmapSubstitute(() => _backgroundBitmap!);

        // Re-sync the editor frame to the freshly aligned selection layer (axis-aligned, no rotation).
        _selectionEditor.SetSelection(
            new NodesSelection(new[] { _selectionLayer }, () => { }) { GenerateOperations = false },
            null);

        _pixelsLifted = true;
        UpdateWorkingBitmapFromSelection();
    }

    private void CommitWorkingBitmapToCanvas()
    {
        if (!_pixelsLifted || DrawingTarget == null) return;

        ApplyWorkingBitmap();

        _workingBitmap?.Clear();
        _swapBitmap?.Clear();
        DrawingTarget.SetTargetBitmapSubstitute(null);
        DrawingTarget.ShowTargetBitmap();

        _pixelsLifted = false;
    }

    public void SetCustomPixelSelector(IPixelSelector pixelSelector)
    {
        _customPixelSelector = pixelSelector;
    }

    public void ClearCustomPixelSelector()
    {
        _customPixelSelector = null;
    }

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
        _deferredTouchSelectionStart = false;

        if (State is DrawingLayerState.Drawing or DrawingLayerState.DrawingSelectionArea)
        {
            CancelDrawing();
        }
    }

    public void AddSelectionPoint(SKPoint p)
    {
        if (_pixelSelector == null)
            return;

        var pivot = SKPoint.Empty;
        GetGlobalTransform().TryInvert(out var invertedTransform);
        var selectionPoint = invertedTransform.MapPoint(p + pivot);
        _pixelSelector.AddSelectionPoint(new SKPointI((int)selectionPoint.X, (int)selectionPoint.Y),
            (x, y) =>
            {
                if (InBounds(x, y))
                    SetPixel(x, y, GetSelectionDashColor(x, y));
            });
    }

    public void SetSelectionRect(SKPoint startPos, SKPoint endPos)
    {
        if (_pixelSelector == null)
        {
            return;
        }

        var pivot = SKPoint.Empty;
        GetGlobalTransform().TryInvert(out var invertedTransform);

        var p1 = invertedTransform.MapPoint(startPos + pivot).ToSkPointI();
        var p2 = invertedTransform.MapPoint(new SKPoint(endPos.X, startPos.Y) + pivot).ToSkPointI();
        var p3 = invertedTransform.MapPoint(endPos + pivot).ToSkPointI();
        var p4 = invertedTransform.MapPoint(new SKPoint(startPos.X, endPos.Y) + pivot).ToSkPointI();

        void Plot(int x, int y)
        {
            if (InBounds(x, y))
                SetPixel(x, y, GetSelectionDashColor(x, y));
        }

        _pixelSelector.BeginSelection(p1);
        _pixelSelector.AddSelectionPoint(p2, Plot);
        _pixelSelector.AddSelectionPoint(p3, Plot);
        _pixelSelector.AddSelectionPoint(p4, Plot);
        _pixelSelector.AddSelectionPoint(p1, Plot);

        var w = Math.Abs(p3.X - p1.X);
        var h = Math.Abs(p3.Y - p1.Y);
        SelectionSize = new SKSizeI(w + 1, h + 1);
        SwapWorkingBitmap();
    }

    protected virtual void OnDrawingApplied(bool saveToUndo)
    {
        DrawingApplied?.Invoke(this, new DrawingAppliedEventArgs(saveToUndo));
    }

    protected virtual void OnPixelsSelected()
    {
        PixelsSelected?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnPixelsBeforeSelected(SKBitmap selectionBitmap)
    {
        PixelsBeforeSelected?.Invoke(this, new PixelsBeforeSelectedEventArgs(selectionBitmap));
    }

    protected virtual void OnSelectionStarted()
    {
        SelectionStarted?.Invoke(this, EventArgs.Empty);
    }

    public void FlipSelection(FlipMode mode)
    {
        _selectionEditor.ManipulateSelection(() =>
        {
            var sl = _selectionLayer;
            if (sl == null)
                return;

            if (mode == FlipMode.Horizontal)
                sl.FlipHorizontal();

            if (mode == FlipMode.Vertical)
                sl.FlipVertical();

            sl.InvalidateBitmap();
        });
    }

    public void RotateSelection(int angle)
    {
        _selectionEditor.Rotate(90);
        //_selectionEditor.ManipulateSelection(() =>
        //{

        //    var sl = _selectionLayer;

        //    sl.RotateSourceBitmap(true);

        //    sl.InvalidateBitmap();
        //});
    }

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

    protected virtual void OnSelectionTransformed(SelectionOperation operation)
    {
        SelectionTransformed?.Invoke(this, new SelectionTransformedEventArgs(operation));
    }

    protected virtual void OnSelectionRemoved() => SelectionRemoved?.Invoke(this, EventArgs.Empty);
}