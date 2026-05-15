using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs.Resize;

public abstract class ResizeThumbSingleNode : NodeManipulateThumbBase, IViewPortBindable
{
    private SKPoint _initialThumbLocalPos;
    protected SKPoint _initialTargetPos;
    protected SKPoint _initialTargetPivotPosition;
    private SKSize _initialTargetSize;

    public SKColor StrokeColor = SKColor.Parse("#ff4384de");

    private SKPoint _initialThumbGlobalPos;
    protected SKMatrix _initialTargetLocalTransform;
    protected SKMatrix _initialTargetGlobalTransform;
    public Func<bool>? AspectLockProviderFunc { get; set; }

    public ResizeThumbSingleNode()
    {
        const float size = 24;
        Size = new SKSize(size, size);
        PivotPosition = new SKPoint(size * 0.5f, size * 0.5f);
        DragStarted += MoveNodeThumb_DragStarted;
        DragDelta += MoveNodeThumb_DragDelta;
    }

    private void MoveNodeThumb_DragStarted(object? sender, DragStartedEventArgs e)
    {
        var frame = TargetSelection!.Frame;
        if (frame == null) return;
        _initialThumbLocalPos = frame.GetLocalPosition(Position);
        _initialThumbGlobalPos = GetGlobalPosition();
        _initialTargetPos = frame.Position;
        _initialTargetPivotPosition = frame.PivotPosition;
        _initialTargetSize = frame.Size;

        _initialTargetGlobalTransform = frame.GetGlobalTransform();
        _initialTargetGlobalTransform.TryInvert(out var invertedWorldTransform);
        _initialTargetLocalTransform = invertedWorldTransform!;
    }

    private void MoveNodeThumb_DragDelta(object? sender, DragDeltaEventArgs e)
    {
        DragNode(this, _initialThumbGlobalPos, new SKPoint(e.HorizontalChange, e.VerticalChange), false);

        var localDelta = _initialTargetLocalTransform.MapPoint(Position) - _initialThumbLocalPos;
        var newX = localDelta.X;
        var newY = localDelta.Y;

        // if (SnapToPixels)
        // {
        //     newX = (float) Math.Floor(localDelta.X);
        //     newY = (float) Math.Floor(localDelta.Y);
        // }

        var delta = new SKPoint(newX, newY);
        if (delta != SKPoint.Empty)
            SetNewBounds(_initialTargetSize, delta, AspectLockProviderFunc?.Invoke() ?? false);
    }

    protected abstract void SetNewBounds(SKSize initialSize, SKPoint delta, bool lockAspect);

    protected SKSize CalculateNewSize(SKSize initialSize, SKPoint delta, bool lockAspect)
    {
        var newW = initialSize.Width + delta.X;
        var newH = initialSize.Height + delta.Y;

        if (lockAspect)
        {
            var aspect = initialSize.GetAspect();

            if (Math.Abs(delta.X) > Math.Abs(delta.Y))
            {
                newH = newW / aspect;
            }
            else
            {
                newW = newH * aspect;
            }
        }

        // newW = (float)Math.Floor(Math.Max(1, newW));
        // newH = (float)Math.Floor(Math.Max(1, newH));
        return new SKSize(newW, newH);
    }

    protected static SKPoint GetSizeDelta(SKSize initialSize, SKSize newSize)
    {
        return new SKPoint(newSize.Width - initialSize.Width, newSize.Height - initialSize.Height);
    }

    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var bounds = selection.Bounds;
        Position = bounds.GetRightBottomPoint()!;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        var hz = GetHitZone();
        var r = Size.Width / 2f;

        using var fillPaint = new SKPaint { Color = SKColors.White };
        using var strokePaint = new SKPaint
        {
            IsStroke = true,
            IsAntialias = true,
            StrokeWidth = vp.PixelsToWorld(2),
            Color = StrokeColor,
        };

        canvas.Save();
        canvas.SetMatrix(vp.ResultTransformMatrix);
        canvas.DrawCircle(hz.MidX, hz.MidY, r, fillPaint);
        canvas.DrawCircle(hz.MidX, hz.MidY, r, strokePaint);
        canvas.Restore();
    }

    public void OnViewChanged(ViewPort vp)
    {
        var size = vp.PixelsToWorld(24) * vp.ScaleFactor;
        Size = new SKSize(size, size);
        PivotPosition = new SKPoint(size / 2f, size / 2f);
        ProjectionTransform = null;
        UpdateToTargets();
    }
}