#nullable enable
using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs;

public class RotateThumbNode : NodeManipulateThumbBase, IViewPortBindable
{
    public SKColor StrokeColor = SKColor.Parse("#ff4384de");
    private SKPoint _initialThumbPos;
    private Dictionary<SKNode, SKPoint> _initialTargetsPos;
    private ViewPort _bindedViewPort;
    private SKPoint _borderMidPoint;

    public Func<bool> AngleLockProviderFunc { get; set; }

    public RotateThumbNode()
    {
        Size = new SKSize(24, 24);
        PivotPosition = new SKPoint(12, 12);

        DragStarted += OnDragStarted;
        DragDelta += OnDragDelta;
        DragComplete += OnDragComplete;

    }

    private void OnDragStarted(object sender, DragStartedEventArgs e)
    {
        _initialThumbPos = GetGlobalPosition();

        _initialTargetsPos = TargetSelection.Nodes.ToDictionary(x => x, x => x.GetGlobalPosition());
    }

    private void OnDragComplete(object sender, DragCompletedEventArgs e)
    {
        _initialTargetsPos = null;
    }

    private void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_initialTargetsPos == null)
            return;

        var delta = new SKPoint(e.HorizontalChange, e.VerticalChange);

        var newPos = _initialThumbPos + delta;

        var frame = TargetSelection.Frame;
        var centerPoint = frame.GetGlobalTransform().MapPoint(new SKPoint(frame.Size.Width / 2f, frame.Size.Height / 2f));

        var angle = (float) ( Math.Atan2(newPos.X - centerPoint.X, newPos.Y - centerPoint.Y) * (180 / Math.PI) );

        if (AngleLockProviderFunc?.Invoke() == true)
        {
            angle = angle.SnapToGrid(5);
        }
        else
        {
            angle = angle.SnapToGrid(1);
        }

        TargetSelection.Rotation = 180f - angle;
    }

    protected override void AdjustDimensionsToTargets(NodesSelection? selection)
    {
        if(selection == null || _bindedViewPort == null)
            return;

        var frame = selection.Frame;
        var x0 = frame.Size.Width / 2f;
        var midPoint = new SKPoint(x0, -_bindedViewPort.PixelsToWorld(64));
        Position = frame.GetGlobalTransform().MapPoint(midPoint);
        _borderMidPoint = frame.GetGlobalTransform().MapPoint(new SKPoint(x0, 0));
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        var hz = GetHitZone();

        canvas.Save();
        var transform = vp.ResultTransformMatrix;
        canvas.SetMatrix(transform);

        using var strokePaint = new SKPaint();
        using var fillPaint = new SKPaint();
        strokePaint.IsStroke = true;
        strokePaint.IsAntialias = true;
        strokePaint.StrokeWidth = vp.PixelsToWorld(2);
        strokePaint.Color = StrokeColor;

        fillPaint.Color = SKColors.White;

        canvas.DrawLine(hz.MidX, hz.MidY, _borderMidPoint.X, _borderMidPoint.Y, strokePaint);
        canvas.DrawCircle(hz.MidX, hz.MidY, vp.PixelsToWorld(12) * vp.ScaleFactor, fillPaint);
        canvas.DrawCircle(hz.MidX, hz.MidY, vp.PixelsToWorld(12) * vp.ScaleFactor, strokePaint);

        canvas.Restore();
    }

    public void OnViewChanged(ViewPort vp)
    {
        _bindedViewPort = vp;

        var size = vp.PixelsToWorld(24);
        Size = new SKSize(size, size);
        PivotPosition = new SKPoint(size/2, size/2);

        AdjustDimensionsToTargets(TargetSelection);
    }
}