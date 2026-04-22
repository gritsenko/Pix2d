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

    public bool ClickThrough { get; set; } = true;

    /// <summary>
    /// When true, the thumb draws a dashed marching-ants contour instead of the solid manipulation border.
    /// Intended for the "selection without transform" state where handles are hidden.
    /// </summary>
    public bool ContourOnly { get; set; }

    public Func<bool> AxisLockProviderFunc { get; set; } = null!;
    public AxisLockMode AxisLockMode { get; set; }

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
                DragNode(target, _initialTargetsPos[target], delta, SnapToPixels);
        }
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (ContourOnly)
        {
            using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1), SKColors.Black);
            paint.PathEffect = SKPathEffect.CreateDash([vp.PixelsToWorld(2), vp.PixelsToWorld(2)], 0);
            canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
        }
        else
        {
            using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(2), StrokeColor);
            canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
        }
    }

}