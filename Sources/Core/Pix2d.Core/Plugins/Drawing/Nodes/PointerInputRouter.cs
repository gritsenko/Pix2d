using Pix2d.Abstract.Drawing;
using Pix2d.Primitives.Drawing;
using Pix2d.Primitives.Edit;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

internal sealed class PointerInputRouter
{
    /// <summary>
    /// Below this much pointer travel (viewport pixels) a marquee gesture counts as a click, i.e. "deselect",
    /// not "select a 1-pixel area". Deliberately much smaller than
    /// <c>DeferredTouchSelection.DragThresholdViewportPixels</c> — this only has to absorb mouse jitter, not
    /// tell a tap apart from a pan.
    /// </summary>
    private const float MarqueeClickThresholdViewportPixels = 4f;

    private readonly IPointerInputRouterHost _host;
    private readonly DeferredTouchSelection _deferredTouchSelection = new();
    private SKPoint _marqueeStartViewportPosition;

    public PointerInputRouter(IPointerInputRouterHost host)
    {
        _host = host;
    }

    public SKPoint LastPointerPosition { get; set; }
    public SKPointI PreviewPosition { get; set; }
    public AxisLockMode AxisLockMode { get; set; }

    public void OnPanModeChanged(bool isPanModeEnabled)
    {
        if (isPanModeEnabled && _deferredTouchSelection.HasPendingSelectionStart)
        {
            _deferredTouchSelection.Cancel();
        }
    }

    public void CancelDeferredSelection()
    {
        _deferredTouchSelection.Cancel();
    }

    public bool ShouldIgnorePointerPressed(PointerActionEventArgs eventArgs)
    {
        if (_host.GetDrawingMode() == BrushDrawingMode.MoveSelection)
            return true;

        if (_host.State == DrawingLayerState.Paste)
        {
            _host.ApplySelection();
        }

        return _host.GetDrawingMode() == BrushDrawingMode.ExternalDraw
            || _host.GetDrawingMode() == BrushDrawingMode.Fill
            || _host.GetDrawingMode() == BrushDrawingMode.FillErase
            || (eventArgs.KeyModifiers & KeyModifier.Alt) != 0;
    }

    public void HandlePointerPressed(PointerActionEventArgs eventArgs)
    {
        if (_host.GetDrawingMode() == BrushDrawingMode.Select)
        {
            HandleSelectionPointerPressed(eventArgs);
        }
        else
        {
            HandleDrawingPointerPressed();
        }
    }

    public bool TryHandleDeferredTouchTapRelease()
    {
        if (!_deferredTouchSelection.ConsumeTapRelease())
            return false;

        if (_host.HasSelection)
        {
            _host.ApplySelection();
            _host.Refresh();
        }

        return true;
    }

    public void HandlePointerReleased(PointerActionEventArgs eventArgs)
    {
        if (_host.IsPointerOverSelection(eventArgs.Pointer.WorldPosition))
        {
            return;
        }

        AxisLockMode = AxisLockMode.None;

        var drawingMode = _host.GetDrawingMode();
        if (drawingMode == BrushDrawingMode.ExternalDraw || drawingMode == BrushDrawingMode.MoveSelection)
        {
            return;
        }

        if (TryFinishSelectionArea(eventArgs))
            return;

        if (TryApplyFillOnRelease())
            return;

        _host.FinishReleasedDrawing();
    }

    public void HandlePointerMoved(PointerActionEventArgs eventArgs, SKPointI prevPointerPosition, SKPointI currPointerPosition)
    {
        if (ShouldWaitForDeferredTouchSelection(eventArgs))
            return;

        if (_host.State == DrawingLayerState.Drawing)
        {
            _host.DrawStroke(GetPointerMoveStrokeEndPosition(prevPointerPosition, currPointerPosition));
            return;
        }

        if (_host.State == DrawingLayerState.DrawingSelectionArea)
        {
            HandleSelectionAreaPointerMoved();
        }
    }

    private void HandleSelectionPointerPressed(PointerActionEventArgs eventArgs)
    {
        if (eventArgs.Pointer.IsTouch)
        {
            _deferredTouchSelection.Begin(eventArgs.Pointer.ViewportPosition);
            return;
        }

        _marqueeStartViewportPosition = eventArgs.Pointer.ViewportPosition;
        _host.CapturePointer();
        _host.BeginSelection(_host.StartPosI);
        _host.AddSelectionPoint(_host.StartPosI);
    }

    private void HandleDrawingPointerPressed()
    {
        _host.CapturePointer();
        if (_host.IsTargetBitmapVisible)
            _host.BeginDrawing();

        if (_host.State == DrawingLayerState.Drawing)
        {
            _host.DrawStroke(LastPointerPosition);
            _host.Refresh();
        }
    }

    private bool TryFinishSelectionArea(PointerActionEventArgs eventArgs)
    {
        if (_host.State != DrawingLayerState.DrawingSelectionArea || _host.GetDrawingMode() != BrushDrawingMode.Select)
            return false;

        if (IsMarqueeClick(eventArgs))
        {
            // A click with a selection tool means "deselect", not "select a single pixel": the press already
            // dropped the previous marquee (BeginSelection → ApplySelection), so all that's left is to throw
            // away the degenerate one this gesture would otherwise produce. A click *inside* a live marquee
            // never reaches here — the frame's move thumb captures the pointer first — so "click the selected
            // area to keep it" still holds.
            _host.CancelMarquee();
            return true;
        }

        _host.AddSelectionPoint(_host.StartPosI);
        _host.FinishSelection();
        return true;
    }

    /// <summary>
    /// True when the finished gesture was a click rather than a marquee drag. The magic wand is exempt: for
    /// <see cref="PixelSelectionMode.SameColor"/> a single click *is* the whole selection gesture.
    /// </summary>
    private bool IsMarqueeClick(PointerActionEventArgs eventArgs)
    {
        if (_host.SelectionMode == PixelSelectionMode.SameColor)
            return false;

        var dx = eventArgs.Pointer.ViewportPosition.X - _marqueeStartViewportPosition.X;
        var dy = eventArgs.Pointer.ViewportPosition.Y - _marqueeStartViewportPosition.Y;
        return dx * dx + dy * dy
            < MarqueeClickThresholdViewportPixels * MarqueeClickThresholdViewportPixels;
    }

    private bool TryApplyFillOnRelease()
    {
        if (_host.GetDrawingMode() == BrushDrawingMode.Fill)
        {
            _host.FillRegion(_host.EndPos, _host.DrawingColor);
            return true;
        }

        if (_host.GetDrawingMode() == BrushDrawingMode.FillErase)
        {
            _host.FillRegion(_host.EndPos, SKColors.White, blendMode: SKBlendMode.DstOut);
            return true;
        }

        return false;
    }

    private bool ShouldWaitForDeferredTouchSelection(PointerActionEventArgs eventArgs)
    {
        if (!_deferredTouchSelection.HasPendingSelectionStart)
            return false;

        if (!_deferredTouchSelection.TryPromote(eventArgs.Pointer.ViewportPosition))
        {
            return true;
        }

        _marqueeStartViewportPosition = eventArgs.Pointer.ViewportPosition;
        _host.CapturePointer();
        _host.BeginSelection(_host.StartPosI);
        _host.AddSelectionPoint(_host.StartPosI);
        LastPointerPosition = _host.StartPosI;
        return false;
    }

    private SKPointI GetPointerMoveStrokeEndPosition(SKPointI prevPointerPosition, SKPointI currPointerPosition)
    {
        var strokeEndPos = _host.EndPosI;
        if (_host.AspectSnapper?.IsAspectLocked != true)
            return strokeEndPos;

        if (AxisLockMode == AxisLockMode.None && currPointerPosition != prevPointerPosition)
        {
            var delta = PreviewPosition - prevPointerPosition;
            AxisLockMode = Math.Abs(delta.X) > Math.Abs(delta.Y) ? AxisLockMode.Horizontal : AxisLockMode.Vertical;
            _host.StartPos = new SKPoint(prevPointerPosition.X, prevPointerPosition.Y);
        }

        if (AxisLockMode == AxisLockMode.Horizontal)
        {
            return new SKPointI(_host.EndPosI.X, _host.StartPosI.Y);
        }

        if (AxisLockMode == AxisLockMode.Vertical)
        {
            return new SKPointI(_host.StartPosI.X, _host.EndPosI.Y);
        }

        return strokeEndPos;
    }

    private void HandleSelectionAreaPointerMoved()
    {
        switch (_host.SelectionMode)
        {
            case PixelSelectionMode.Rectangle:
                _host.SetSelectionRect(_host.StartPosI, _host.EndPosI);
                break;
            case PixelSelectionMode.Freeform:
                _host.AddSelectionPoint(_host.EndPos);
                break;
            case PixelSelectionMode.SameColor:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}