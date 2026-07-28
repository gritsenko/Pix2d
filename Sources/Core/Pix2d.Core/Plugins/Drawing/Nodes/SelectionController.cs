using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Selection;
using Pix2d.InteractiveNodes;
using Pix2d.Plugins.Drawing.PixelSelectors;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Primitives.Drawing;
using Pix2d.Primitives.Edit;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// Owns the pixel-selection state and behavior previously embedded in
/// <see cref="DrawingLayerNode"/>: the selection layer, the <see cref="FrameEditorNode"/>,
/// the pixel selector, and the lift/transform tracking. Talks back to the node only through
/// <see cref="ISelectionLayerHost"/>. Extraction is incremental — see
/// .plans/drawing-layer-selection-controller.md for which pieces have landed.
/// </summary>
internal sealed class SelectionController
{
    private readonly ISelectionLayerHost _host;
    private readonly DrawingLayerNode _ownerNode;

    // The fields below used to live on DrawingLayerNode. Some still need to be read/written by
    // methods that haven't moved yet (BeginSelection, ApplySelection, Activate/Deactivate editor,
    // Lift/Commit, etc.); for those we expose narrow `internal` accessors. As subsequent steps
    // pull those methods into the controller, the accessors will tighten to `private`.
    private SpriteSelectionNode? _selectionLayer;
    private readonly FrameEditorNode _selectionEditor;
    private readonly SelectionMarqueeOverlayNode _marqueeOverlay;
    private IPixelSelector? _pixelSelector;
    private IPixelSelector? _customPixelSelector;
    private TransformSelectionOperation? _currentSelectionOperation;
    private bool _pixelsLifted;
    private bool _frameResizeMode;

    public event EventHandler? SelectionStarted;
    public event EventHandler? SelectionRemoved;
    public event EventHandler? PixelsSelected;
    public event EventHandler<PixelsBeforeSelectedEventArgs>? PixelsBeforeSelected;
    public event EventHandler<SelectionTransformedEventArgs>? SelectionTransformed;

    /// <summary>
    /// Fires from <see cref="FinishSelection"/> only — i.e. when a user actually completes a fresh
    /// marquee gesture (Rect/Lasso/Color drag, Select-All). Distinct from <see cref="PixelsSelected"/>,
    /// which also fires from <see cref="SetSelection"/> during undo/redo / paste replay. Used by
    /// <c>DrawingService</c> to push <c>BeginSelectionOperation</c> exactly once per real selection.
    /// </summary>
    public event EventHandler? MarqueeFinishedByUser;

    public PixelSelectionMode SelectionMode { get; set; }
    public SKSize SelectionSize { get; set; }

    public bool HasSelection => _selectionLayer != null;
    public bool HasSelectionChanges => _selectionEditor.IsChanged;

    /// <summary>
    /// Derived from <see cref="HasSelection"/> + the lifted-pixels flag. Source of truth for
    /// tools deciding what selection-related actions are valid right now.
    /// </summary>
    public SelectionPhase SelectionPhase => !HasSelection
        ? SelectionPhase.None
        : _pixelsLifted
            ? SelectionPhase.Transforming
            : SelectionPhase.MarqueeReady;

    public bool IsEditorVisible => _selectionEditor.IsVisible;
    public bool SelectionBoundsContains(SKPoint worldPos)
        => _selectionEditor.IsVisible && _selectionEditor.SelectionBounds.Contains(worldPos);

    /// <summary>
    /// Called when the drawing target wants any in-flight selection edit to be committed (e.g.
    /// before switching frames). Mirrors the legacy <c>FlushCurrentEditing</c> on the node.
    /// </summary>
    public void FlushCurrentEditing()
    {
        if (!_selectionEditor.IsVisible) return;
        if (_selectionEditor.IsChanged)
            ApplySelection();
        else
            CancelSelect();
    }

    public SelectionController(DrawingLayerNode ownerNode)
    {
        _ownerNode = ownerNode;
        _host = ownerNode;

        _selectionEditor = new FrameEditorNode
        {
            IsVisible = false,
            // Resize handles are hidden by default — Rect/Lasso/Color marquee tools never show
            // them (reshaping a marquee belongs to a future crop tool). They get re-enabled when
            // the user enters transform mode on lifted pixels (see ApplyEditorMode).
            AllowResize = false,
        };
        _selectionEditor.SelectionEditStarted += SelectionEditor_SelectionEditStarted;
        _selectionEditor.SelectionEdited += SelectionEditor_SelectionEdited;
        _selectionEditor.SelectionEditing += SelectionEditor_SelectionEditing;
        _selectionEditor.SelectionEditCanceled += SelectionEditor_SelectionEditCanceled;
        _selectionEditor.AspectSnapperProviderFunc = () => _host.AspectSnapper!;

        _marqueeOverlay = new SelectionMarqueeOverlayNode { IsVisible = false };
    }

    public SKNode GetSelectionLayerNode()
        => _selectionLayer ?? throw new InvalidOperationException("Selection layer is not initialized");

    public void SetCustomPixelSelector(IPixelSelector pixelSelector) => _customPixelSelector = pixelSelector;
    public void ClearCustomPixelSelector() => _customPixelSelector = null;

    public void InvalidateSelectionEditor()
    {
        if (_selectionLayer != null)
            _selectionEditor.SetSelection(
                new NodesSelection(new[] { _selectionLayer }, () => { }) { GenerateOperations = false },
                _selectionLayer.SelectionPath,
                _selectionLayer.SelectionContours);
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
        _selectionEditor.Rotate(angle);
    }

    /// <summary>
    /// Sets the content of working bitmap to the selection layer contents.
    /// </summary>
    private void UpdateWorkingBitmapFromSelection()
    {
        if (!HasSelection)
            return;

        using var canvas = new SKCanvas(_host.WorkingBitmap);

        var target = ((SKNode)_host.DrawingTarget!).Position;

        var vp = new ViewPort((int)_host.Size.Width, (int)_host.Size.Height);
        vp.SetPan(target.X, target.Y);

        SKNodeRenderer.Render(_selectionLayer!, new RenderContext(canvas, vp));
        canvas.Flush();
        _host.WorkingBitmap.NotifyPixelsChanged();
        _host.SwapWorkingBitmap();

        // Publish an immutable COW snapshot of the freshly-swapped working bitmap so the compositor
        // reads stable pixels even if the next drag delta starts modifying the bitmap before this
        // frame finishes painting. Without this, pointer events arriving faster than the paint rate
        // caused the compositor to read the bitmap mid-write, showing as horizontal tear bands.
        _host.PromoteWorkingBitmapToDisplay();
    }

    private TransformSelectionOperation GetCurrentSelectionOperationOrNew()
    {
        return _currentSelectionOperation == null
            ? new TransformSelectionOperation(_ownerNode, _host.ActiveToolKeyProvider?.Invoke())
            : new TransformSelectionOperation(_currentSelectionOperation);
    }

    private void SelectionEditor_SelectionEditing(object? sender, EventArgs e)
    {
        if (_pixelsLifted)
            UpdateWorkingBitmapFromSelection();
        else
            _host.RequestRefresh();
    }

    private void SelectionEditor_SelectionEdited(object? sender, EventArgs e)
    {
        _currentSelectionOperation!.SetFinalState(_host.ActiveToolKeyProvider?.Invoke());
        if (_pixelsLifted)
            UpdateWorkingBitmapFromSelection();
        else
            _host.RequestRefresh();
        OnSelectionTransformed(_currentSelectionOperation);
    }

    private void SelectionEditor_SelectionEditStarted(object? sender, EventArgs e)
    {
        _currentSelectionOperation = GetCurrentSelectionOperationOrNew();
        if (_pixelsLifted)
            UpdateWorkingBitmapFromSelection();
    }

    private void SelectionEditor_SelectionEditCanceled(object? sender, EventArgs e)
    {
        _currentSelectionOperation = null;
        _host.RequestRefresh();
    }

    private void EnsureMarqueeOverlayAttached()
    {
        var drawingTarget = _host.DrawingTarget;
        if (drawingTarget is not SKNode targetNode)
            return;

        var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(targetNode.Parent ?? targetNode);
        if (_marqueeOverlay.Parent != adornerLayer)
            adornerLayer.Add(_marqueeOverlay);

        _marqueeOverlay.Position = targetNode.Position;
    }

    private void ShowMarqueeOverlay()
    {
        EnsureMarqueeOverlayAttached();
        _marqueeOverlay.Clear();
        _marqueeOverlay.IsVisible = true;
    }

    private void HideMarqueeOverlay()
    {
        _marqueeOverlay.IsVisible = false;
        _marqueeOverlay.Clear();
        _host.RequestRefresh();
    }

    public SKBitmap GetSelectionBackground()
        => _host.BackgroundBitmap?.Copy() ?? throw new InvalidOperationException("BackgroundBitmap is not initialized");

    public void SelectAll()
    {
        ApplySelection();
        OnSelectionStarted();
        _pixelSelector = new AllPixelSelector();
        FinishSelection();
    }

    public void BeginSelection(SKPoint pos)
    {
        ApplySelection();
        OnSelectionStarted();
        _host.State = DrawingLayerState.DrawingSelectionArea;
        _host.ClearWorkingBuffers();
        _host.WorkingBitmap.NotifyPixelsChanged();

        var drawingTarget = _host.DrawingTarget;
        if (drawingTarget != null && SelectionMode == PixelSelectionMode.SameColor)
        {
            var size = drawingTarget.GetSize();
            var bitmap = new SKBitmap(new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Rgba8888));
            drawingTarget.CopyBitmapTo(bitmap);
            _pixelSelector = new SameColorSelector(bitmap, _ownerNode.ColorSelectionTolerance, _ownerNode.ColorSelectionScope);
        }
        else
        {
            _pixelSelector = _customPixelSelector ?? new PixelSelector();
        }

        // Working bitmap is no longer used to host the marching-ants visualization (the
        // SelectionMarqueeOverlayNode draws those as vectors). Keep UseSwapBitmap false so
        // OnDraw doesn't show stale swap content during the drag.
        _host.UseSwapBitmap = false;

        // pos arrives in world space (the router hands over the pointer's press position), while the
        // selectors work in layer-local pixels — the same mapping AddSelectionPoint/SetSelectionRect do.
        // Without it an artboard that isn't at the scene origin seeded the selector with an off-canvas
        // point: the magic wand sampled the wrong pixel, and PixelSelector ran its first LineDda from
        // there, dragging a stray line into the selection (which is how a plain click could come out as a
        // wide rectangle).
        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        var localStart = invertedTransform.MapPoint(pos);
        _pixelSelector.BeginSelection(new SKPointI((int)localStart.X, (int)localStart.Y));

        ShowMarqueeOverlay();

        if (SelectionMode != PixelSelectionMode.Rectangle)
        {
            // Freeform marquee: seed the visual path with the starting point so the first move-event
            // already produces a visible line segment, matching the pointer trace.
            _marqueeOverlay.BeginFreeformPath(localStart);
        }
    }

    public void AddSelectionPoint(SKPoint p)
    {
        if (_pixelSelector == null)
            return;

        var pivot = SKPoint.Empty;
        _host.GetGlobalTransform().TryInvert(out var invertedTransform);
        var selectionPoint = invertedTransform.MapPoint(p + pivot);

        // Pass a no-op plot: the pixel-level visualization is now handled by SelectionMarqueeOverlayNode.
        // The selector still needs the call to keep _selectionPoints populated (used by FinishSelection
        // to build the actual selection bitmap and contour).
        _pixelSelector.AddSelectionPoint(new SKPointI((int)selectionPoint.X, (int)selectionPoint.Y), NoPlot);

        _marqueeOverlay.AddFreeformPoint(selectionPoint);
        _host.RequestRefresh();
    }

    public void SetSelectionRect(SKPoint startPos, SKPoint endPos)
    {
        if (_pixelSelector == null)
            return;

        var pivot = SKPoint.Empty;
        _host.GetGlobalTransform().TryInvert(out var invertedTransform);

        var p1 = invertedTransform.MapPoint(startPos + pivot).ToSkPointI();
        var p2 = invertedTransform.MapPoint(new SKPoint(endPos.X, startPos.Y) + pivot).ToSkPointI();
        var p3 = invertedTransform.MapPoint(endPos + pivot).ToSkPointI();
        var p4 = invertedTransform.MapPoint(new SKPoint(startPos.X, endPos.Y) + pivot).ToSkPointI();

        _pixelSelector.BeginSelection(p1);
        _pixelSelector.AddSelectionPoint(p2, NoPlot);
        _pixelSelector.AddSelectionPoint(p3, NoPlot);
        _pixelSelector.AddSelectionPoint(p4, NoPlot);
        _pixelSelector.AddSelectionPoint(p1, NoPlot);

        var w = Math.Abs(p3.X - p1.X);
        var h = Math.Abs(p3.Y - p1.Y);
        SelectionSize = new SKSizeI(w + 1, h + 1);

        _marqueeOverlay.SetRectanglePath(p1, p3);
        _host.RequestRefresh();
    }

    private static void NoPlot(int x, int y) { }

    /// <summary>
    /// Activates selection editor using currently drawn selection area. If nothing is selected
    /// (1 pixel is in the selected area), editor is not activated.
    /// </summary>
    public void FinishSelection()
    {
        var drawingTarget = _host.DrawingTarget;
        if (_pixelSelector == null || drawingTarget == null)
            return;

        // Drag-phase visualization is done — the static frame (FrameEditorNode + LineHighlightNode) takes over below.
        HideMarqueeOverlay();

        _host.State = DrawingLayerState.Ready;

        var selector = _pixelSelector;
        selector.FinishSelection(SelectionMode != PixelSelectionMode.Rectangle);

        var size = drawingTarget.GetSize();
        var tmpBitmap = new SKBitmap(new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Rgba8888));
        drawingTarget.CopyBitmapTo(tmpBitmap);
        var selectionBitmap = selector.GetSelectionBitmap(tmpBitmap);

        selector.ClearSelectionFromBitmap(ref tmpBitmap);

        if (selectionBitmap.Pixels.Length > 1)
        {
            OnPixelsBeforeSelected(selectionBitmap);

            _selectionLayer = new SpriteSelectionNode
            {
                Bitmap = selectionBitmap,
                SelectionPath = selector.GetSelectionPath(),
                SelectionContours = selector.GetSelectionContours(),
                Opacity = 1,
                Position = selector.Offset + ((SKNode)drawingTarget).Position,
            };

            _host.Opacity = drawingTarget.GetOpacity();
            _host.BackgroundBitmap = tmpBitmap;

            // Selection tools (Rect / Lasso / Color) always finish in contour-only mode — they
            // never lift pixels. Auto-enter into transform mode (if enabled) is handled by
            // DrawingService switching to PixelTransformTool, which is the single owner of the
            // "pixels lifted" state.
            ActivateEditor(contourOnly: true);
            OnPixelsSelected();
            // Fires last, after the marquee is fully visible. DrawingService listens for this
            // exact event (not PixelsSelected) to push BeginSelectionOperation — keeps undo-stack
            // pushes scoped to actual user gestures rather than every SetSelection replay.
            MarqueeFinishedByUser?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplySelection(bool saveToUndo = false)
    {
        _pixelSelector = null;
        if (_selectionLayer == null)
            return;

        // In contour-edit mode pixels aren't lifted, so the working bitmap doesn't carry the
        // transformed state — applying must not stamp it onto the target (that would just erase
        // the marquee's source area). The contour-mode marquee changes only reshape the selection
        // region, not the image.
        if (_pixelsLifted && _selectionEditor.IsChanged)
        {
            _host.ApplyWorkingBitmap();
            _host.RaiseDrawingApplied(saveToUndo);
        }

        DeactivateSelectionEditor();
    }

    public void EraseSelection()
    {
        _pixelSelector = null;
        if (_selectionLayer == null)
            return;

        // Working-bitmap-only clear (background stays — DeactivateSelectionEditor wipes it).
        _host.ClearWorkingAndSwapBitmaps();
        _host.ApplyWorkingBitmap();
        DeactivateSelectionEditor();
    }

    public void CancelSelect() => DeactivateSelectionEditor();

    /// <summary>
    /// Called when a marquee gesture ends without producing a selection — interrupted (pinch, escape) or
    /// resolved as a plain click. Drops the in-progress pixel selector and removes the vector marquee overlay
    /// so nothing stale stays on screen. No-op when no marquee is in flight.
    /// </summary>
    public void CancelMarqueeDrag()
    {
        if (!_marqueeOverlay.IsVisible)
            return;
        _pixelSelector = null;
        HideMarqueeOverlay();

        // BeginSelection announced the gesture with SelectionStarted; without the matching event the
        // consumers that track "is the user selecting right now" (selection-size readout in the info panel,
        // tool settings enable-state, clipboard command behaviours) would stay stuck in selecting state.
        OnSelectionRemoved();
    }

    public void DeactivateSelectionEditor()
    {
        _selectionEditor.Hide();
        HideMarqueeOverlay();

        _selectionLayer = null;
        _currentSelectionOperation = null;

        _host.ClearWorkingBuffers();
        _host.ClearDisplaySnapshot();

        _host.UseSwapBitmap = false;
        _host.Opacity = 1;

        _host.DrawingTarget?.SetTargetBitmapSubstitute(null);
        _host.DrawingTarget?.ShowTargetBitmap();

        OnSelectionRemoved();
        _host.RaiseDrawingApplied(false);

        _host.State = DrawingLayerState.Ready;
    }

    /// <summary>
    /// Default editor activation — defaults to transform mode for backwards-compat with callers
    /// that conceptually want the "lifted" experience (e.g. paste / undo of selection ops).
    /// Selection tools call the overload with <c>contourOnly: true</c> explicitly.
    /// </summary>
    public void ActivateEditor() => ActivateEditor(contourOnly: false);

    public void ActivateEditor(bool contourOnly)
    {
        var drawingTarget = _host.DrawingTarget;
        if (drawingTarget is SKNode target && _selectionLayer != null)
        {
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(target.Parent ?? target);
            adornerLayer.Add(_selectionEditor);

            var selection = new NodesSelection(new[] { _selectionLayer }, () => { }) { GenerateOperations = false };

            _selectionEditor.SetSelection(selection, _selectionLayer.SelectionPath, _selectionLayer.SelectionContours);
            ApplyEditorMode(contourOnly);
            _selectionEditor.IsVisible = true;

            _host.UseSwapBitmap = true;

            if (contourOnly)
            {
                // Marquee-only mode: don't lift pixels. Working bitmap stays empty so the
                // underlying canvas is shown unchanged, and dragging the thumbs only reshapes
                // the selection.
                _pixelsLifted = false;
                _host.ClearWorkingAndSwapBitmaps();
                drawingTarget.SetTargetBitmapSubstitute(null);
            }
            else
            {
                _pixelsLifted = true;
                UpdateWorkingBitmapFromSelection();
                if (_host.BackgroundBitmap != null)
                {
                    drawingTarget.SetTargetBitmapSubstitute(() => _host.BackgroundBitmap!);
                }
            }
        }
    }

    public void SetSelectionTransformMode(bool transformMode)
    {
        if (_selectionLayer == null || !_selectionEditor.IsVisible)
            return;

        var contourOnly = !transformMode;
        if (_selectionEditor.ContourOnly == contourOnly)
            return;

        if (transformMode)
            LiftSelectionFromCanvas();
        else
            CommitWorkingBitmapToCanvas();

        ApplyEditorMode(contourOnly);
        _host.RequestRefresh();
    }

    /// <summary>
    /// Single point where the editor's mode flags are kept consistent. Contour mode (marquee tools)
    /// hides resize thumbs; transform mode (lifted pixels) exposes them so the user can scale. The
    /// crop tool's frame-resize mode is a third layer on top of contour mode — it keeps the pixels
    /// untouched but forces the resize handles visible (rendered in black) so the user can adjust
    /// the crop rectangle.
    /// </summary>
    private void ApplyEditorMode(bool contourOnly)
    {
        _selectionEditor.ContourOnly = contourOnly;
        _selectionEditor.FrameResizeMode = _frameResizeMode;
        _selectionEditor.AllowResize = !contourOnly || _frameResizeMode;
    }

    /// <summary>
    /// Toggles crop-tool frame-resize mode. While enabled, every subsequent <see cref="ApplyEditorMode"/>
    /// keeps the resize handles visible (in contour styling) on top of contour mode, so the user can
    /// resize the marquee through tool switches and through fresh marquees they draw. Disabled when
    /// the crop tool deactivates.
    /// </summary>
    public void SetFrameResizeMode(bool enabled)
    {
        if (_frameResizeMode == enabled) return;
        _frameResizeMode = enabled;
        if (_selectionEditor.IsVisible)
            ApplyEditorMode(_selectionEditor.ContourOnly);
    }

    private void LiftSelectionFromCanvas()
    {
        var drawingTarget = _host.DrawingTarget;
        if (_pixelsLifted || drawingTarget == null || _selectionLayer == null)
            return;

        var size = drawingTarget.GetSize();
        var snapshot = new SKBitmap(new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Rgba8888));
        drawingTarget.CopyBitmapTo(snapshot);

        var bounds = _selectionLayer.GetBoundingBox();
        var origin = ((SKNode)drawingTarget).Position;
        int x = (int)Math.Round(bounds.Left - origin.X);
        int y = (int)Math.Round(bounds.Top - origin.Y);
        int w = (int)Math.Round(bounds.Width);
        int h = (int)Math.Round(bounds.Height);
        if (w <= 0 || h <= 0)
            return;

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
        _selectionLayer.SelectionContours = null;
        _selectionLayer.Rotation = 0;

        _host.BackgroundBitmap = snapshot;
        drawingTarget.SetTargetBitmapSubstitute(() => _host.BackgroundBitmap!);

        // Re-sync the editor frame to the freshly aligned selection layer (axis-aligned, no rotation).
        _selectionEditor.SetSelection(
            new NodesSelection(new[] { _selectionLayer }, () => { }) { GenerateOperations = false },
            null);

        _pixelsLifted = true;
        UpdateWorkingBitmapFromSelection();
    }

    private void CommitWorkingBitmapToCanvas()
    {
        var drawingTarget = _host.DrawingTarget;
        if (!_pixelsLifted || drawingTarget == null)
            return;

        _host.ApplyWorkingBitmap();

        _host.ClearWorkingAndSwapBitmaps();
        _host.ClearDisplaySnapshot();
        drawingTarget.SetTargetBitmapSubstitute(null);
        drawingTarget.ShowTargetBitmap();

        _pixelsLifted = false;
    }

    public void SetSelection(SpriteSelectionNode selectionLayer, SKBitmap? backgroundBitmap, bool contourOnly = false)
    {
        var drawingTarget = _host.DrawingTarget;
        if (drawingTarget == null)
            return;

        OnSelectionStarted();
        _host.ClearWorkingBuffers();

        _selectionLayer = selectionLayer;

        if (backgroundBitmap == null)
        {
            if (_host.BackgroundBitmap != null)
                drawingTarget.CopyBitmapTo(_host.BackgroundBitmap);
            _host.State = DrawingLayerState.Paste;
        }
        else
        {
            _host.State = DrawingLayerState.Ready;
            _host.BackgroundBitmap = backgroundBitmap.Copy();
        }

        _host.Opacity = drawingTarget.GetOpacity();

        // In transform mode the lifted pixels need to be rendered onto the working bitmap; in
        // contour mode they stay on the target and the working bitmap stays empty (marching ants only).
        if (!contourOnly)
            UpdateWorkingBitmapFromSelection();

        ActivateEditor(contourOnly: contourOnly);

        _selectionEditor.SetIsChanged();
        OnPixelsSelected();
    }

    public void SetSelectionFromExternal(SKBitmap bitmap, in SKPoint position)
    {
        var drawingTarget = _host.DrawingTarget;
        if (drawingTarget == null)
            return;

        var layer = new SpriteSelectionNode
        {
            Bitmap = bitmap,
            Opacity = 1,
            Position = position + ((SKNode)drawingTarget).Position,
            Size = bitmap.Info.Size,
        };

        SetSelection(layer, null);
    }

    public void FillSelection(SKColor color)
    {
        if (!HasSelection || _selectionLayer == null)
            return;

        using (var canvas = new SKCanvas(_host.WorkingBitmap))
        {
            canvas.Clear();

            var blendMode = SKBlendMode.SrcOver;
            if (_host.LockTransparentPixels && _selectionLayer.Bitmap != null)
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
                // TODO: There is a bug here. If selection is scaled by not right bottom handle, or
                // if rotation and scale applied, position of the fill will be wrong.
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

        // Original code compared `WorkingBitmap == _swapBitmap` to detect that the swap bitmap is
        // currently the "live" one; that's equivalent to `UseSwapBitmap` in practice (the live
        // bitmap is whichever the flag selects). Swap to materialize the fill into the working
        // bitmap that ApplyWorkingBitmap will consume.
        if (_host.UseSwapBitmap)
            _host.SwapWorkingBitmap();

        _selectionEditor.SetIsChanged();

        // Stamp the fill onto the target ourselves instead of going through ApplySelection: a
        // marquee selection is contour-only (`_pixelsLifted == false`), and ApplySelection skips
        // ApplyWorkingBitmap in that case so that merely dismissing a marquee never erases its
        // source pixels. FillSelection, however, *did* populate the working bitmap and must commit
        // it — otherwise the fill is silently discarded by DeactivateSelectionEditor.
        _host.ApplyWorkingBitmap();
        _host.RaiseDrawingApplied(true);

        DeactivateSelectionEditor();
    }

    private void OnSelectionStarted() => SelectionStarted?.Invoke(this, EventArgs.Empty);
    private void OnSelectionRemoved() => SelectionRemoved?.Invoke(this, EventArgs.Empty);
    private void OnPixelsSelected() => PixelsSelected?.Invoke(this, EventArgs.Empty);
    private void OnPixelsBeforeSelected(SKBitmap selectionBitmap)
        => PixelsBeforeSelected?.Invoke(this, new PixelsBeforeSelectedEventArgs(selectionBitmap));
    private void OnSelectionTransformed(TransformSelectionOperation operation)
        => SelectionTransformed?.Invoke(this, new SelectionTransformedEventArgs(operation));
}
