using Pix2d.Abstract.Selection;
using Pix2d.Primitives.Edit;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs;

public class MoveThumbNode : NodeManipulateThumbBase
{
    //public SKColor StrokeColor = SKColor.Parse("#ffff84de");
    public SKColor StrokeColor = SKColor.Parse("#ff4384de");

    private SKPoint _initialThumbPos;
    private Dictionary<SKNode, SKPoint>? _initialTargetsPos;
    private SKPoint _initialFramePos;
    private SKPath? _customContourPath;

    public bool ClickThrough { get; set; } = true;

    /// <summary>
    /// When true, the thumb draws a dashed marching-ants contour instead of the solid manipulation border.
    /// Intended for the "selection without transform" state where handles are hidden.
    /// </summary>
    public bool ContourOnly { get; set; }

    /// <summary>
    /// Set to true when a sibling node (e.g. <see cref="Pix2d.CommonNodes.LineHighlightNode"/>) is already rendering
    /// the real selection contour. In that case the move thumb skips its own bounding-rect outline in
    /// <see cref="ContourOnly"/> mode — otherwise a non-rectangular selection (lasso/same-colour) would show
    /// both the true contour and a redundant rectangle around it.
    /// </summary>
    public bool HasCustomContour { get; set; }

    public void SetCustomContourPath(SKPath? path)
    {
        _customContourPath?.Dispose();
        _customContourPath = path;
    }

    public Func<bool> AxisLockProviderFunc { get; set; } = null!;
    public AxisLockMode AxisLockMode { get; set; }

    /// <summary>
    /// When true, a single-click press with Shift held falls through (no capture, no drag session) so the
    /// tool behind the frame receives it — the object-manipulation tool needs Shift+click on a selected
    /// node to toggle it out of the selection (Figma-style). Kept false for pixel-selection marquees,
    /// where Shift has its own meanings.
    /// </summary>
    public bool PassShiftPressThrough { get; set; }

    /// <summary>
    /// When true, a single-click press holding a marquee-combining modifier (Shift = add, Ctrl = subtract)
    /// falls through the same way, so Shift/Ctrl+drag started *inside* a live marquee grows or shrinks it
    /// instead of moving it. Subtracting in particular almost always starts inside the selection, so
    /// without this the gesture is unreachable. Enabled only for a contour-mode pixel selection: once
    /// pixels are lifted the modifiers belong to the transform (Shift = aspect lock).
    /// </summary>
    public bool PassSelectionCombinePressThrough { get; set; }

    public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
        if (clickCount == 1 && ShouldPassPressThrough(eventArgs.KeyModifiers))
            return;

        base.OnPointerPressed(eventArgs, clickCount);
    }

    private bool ShouldPassPressThrough(KeyModifier modifiers)
    {
        if (PassShiftPressThrough && modifiers.HasFlag(KeyModifier.Shift))
            return true;

        return PassSelectionCombinePressThrough
               && (modifiers.HasFlag(KeyModifier.Shift) || modifiers.HasFlag(KeyModifier.Ctrl));
    }

    public MoveThumbNode()
    {
        DragStarted += MoveNodeThumb_DragStarted;
        DragDelta += MoveNodeThumb_DragDelta;
        DragComplete += MoveThumbNode_DragComplete;
    }

    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var frame = selection.Frame;
        PivotPosition = frame?.PivotPosition ?? default;
        Position = frame?.Position ?? default;
        Size = frame?.Size ?? default;


        Rotation = selection.Rotation;
    }

    public override bool ContainsPoint(SKPoint worldPos)
    {
        if (ContourOnly && HasCustomContour && _customContourPath != null)
        {
            var localPos = GetLocalPosition(worldPos);
            return _customContourPath.Contains(localPos.X, localPos.Y);
        }

        return base.ContainsPoint(worldPos);
    }

    private void MoveThumbNode_DragComplete(object? sender, DragCompletedEventArgs e)
    {
        _initialTargetsPos = new();
    }

    public override void OnPointerReleased(PointerActionEventArgs eventArgs)
    {
        AxisLockMode = AxisLockMode.None;

        base.OnPointerReleased(eventArgs);
        //if (ClickThrough)
        //{
        //    var dragDelta = EndPos - StartPos;
        //    if (dragDelta.Length < 2)
        //        eventArgs.Handled = false;
        //}
    }

    private void MoveNodeThumb_DragStarted(object? sender, DragStartedEventArgs e)
    {
        _initialThumbPos = GetGlobalPosition();

        _initialTargetsPos = TargetSelection?.Nodes?.ToDictionary(x => x, x => x.GetGlobalPosition());
        _initialFramePos = TargetSelection?.Frame?.Position ?? default;
    }

    private void MoveNodeThumb_DragDelta(object? sender, DragDeltaEventArgs e)
    {
        if (_initialTargetsPos == null)
            return;

        var delta = new SKPoint(e.HorizontalChange, e.VerticalChange);

        if (AxisLockProviderFunc?.Invoke() == true)
        {
            if (AxisLockMode == AxisLockMode.None)
                AxisLockMode = Math.Abs(delta.X) > Math.Abs(delta.Y) ? AxisLockMode.Horizontal : AxisLockMode.Vertical;

            if (AxisLockMode == AxisLockMode.Horizontal)
                delta = new SKPoint(e.HorizontalChange, 0);

            if (AxisLockMode == AxisLockMode.Vertical)
                delta = new SKPoint(0, e.VerticalChange);
        }
        else
        {
            //if key was released - drop last lock mode
            AxisLockMode = AxisLockMode.None;
        }

        if (TargetSelection?.Frame != null)
            DragNode(TargetSelection.Frame, _initialFramePos, delta, SnapToPixels);

        DragNode(this, _initialThumbPos, delta, SnapToPixels);
        if (TargetSelection?.Nodes != null && _initialTargetsPos != null)
        {
            foreach (var target in TargetSelection.Nodes)
            {
                // The snapshot is taken once, on DragStarted — but the selection is not frozen for the
                // duration of a drag: a tool can rebuild it mid-gesture (the pixel-transform tool recreates
                // its SpriteSelectionNode), and DragComplete empties the snapshot while a stale pointer-move
                // can still arrive. Indexing it directly turned either case into a KeyNotFoundException out
                // of an ordinary drag (appstat, 3.11.3). Adopt the newcomer instead, back-dating its origin
                // by the delta already applied so it stays where it is now and tracks the rest of the drag.
                if (!_initialTargetsPos.TryGetValue(target, out var initialPos))
                {
                    var current = target.GetGlobalPosition();
                    initialPos = new SKPoint(current.X - delta.X, current.Y - delta.Y);
                    _initialTargetsPos[target] = initialPos;
                }

                DragNode(target, initialPos, delta, SnapToPixels);
            }
        }
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (ContourOnly)
        {
            // Non-rectangular selections (lasso, same-colour) render their real outline via LineHighlightNode —
            // drawing the bounding rect here on top of that would defeat the point of a contour-only mode.
            if (HasCustomContour)
                return;

            // Photoshop-style marching ants: two offset dashed strokes (black + white) so the
            // outline stays visible regardless of canvas colour. Path effects must be disposed —
            // paint.PathEffect setter doesn't take ownership, so each OnDraw would otherwise leak
            // a managed handle per frame.
            var dashLen = SelectionOutlineMetrics.GetDashLengthWorld(vp);
            var strokeWidth = SelectionOutlineMetrics.GetStrokeWidthWorld(vp);
            using var blackPaint = canvas.GetSimpleStrokePaint(strokeWidth, SKColors.Black);
            using var whitePaint = canvas.GetSimpleStrokePaint(strokeWidth, SKColors.White);
            using var blackDash = SKPathEffect.CreateDash([dashLen, dashLen], 0);
            using var whiteDash = SKPathEffect.CreateDash([dashLen, dashLen], dashLen);
            blackPaint.PathEffect = blackDash;
            whitePaint.PathEffect = whiteDash;
            var rect = new SKRect(0, 0, Size.Width, Size.Height);
            canvas.DrawRect(rect, blackPaint);
            canvas.DrawRect(rect, whitePaint);
        }
        else
        {
            using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(2), StrokeColor);
            canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
        }
    }

    public override void OnUnload()
    {
        _customContourPath?.Dispose();
        _customContourPath = null;
        base.OnUnload();
    }

}