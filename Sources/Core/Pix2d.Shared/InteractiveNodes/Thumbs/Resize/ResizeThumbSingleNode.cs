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
    private SKSize _actualSize;

    public SKColor StrokeColor = SKColor.Parse("#ff4384de");
    private SKPoint _initialThumbGlobalPos;
    protected SKMatrix _initialTargetLocalTransform;
    protected SKMatrix _initialTargetGlobalTransform;
    public Func<bool> AspectLockProviderFunc { get; set; }

    public ResizeThumbSingleNode()
    {
        const float size = 24;
        Size = new SKSize(size, size);
        PivotPosition = new SKPoint(size * 0.5f, size * 0.5f);
        DragStarted += MoveNodeThumb_DragStarted;
        DragDelta += MoveNodeThumb_DragDelta;
    }

    private void MoveNodeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _initialThumbLocalPos = TargetSelection.Frame.GetLocalPosition(Position);
        _initialThumbGlobalPos = GetGlobalPosition();
        var frame = TargetSelection.Frame;
        _initialTargetPos = frame.Position;
        _initialTargetPivotPosition = frame.PivotPosition;
        _initialTargetSize = frame.Size;

        _initialTargetGlobalTransform = frame.GetGlobalTransform();
        _initialTargetGlobalTransform.TryInvert(out var invertedWorldTransform);
        _initialTargetLocalTransform = invertedWorldTransform;
    }

    private void MoveNodeThumb_DragDelta(object sender, DragDeltaEventArgs e)
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

            if (delta.X > delta.Y)
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

    protected override void AdjustDimensionsToTargets(NodesSelection selection)
    {
        var bounds = selection.Bounds;
        Position = bounds.GetRightBottomPoint();
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        //canvas.SaveAsync();
        //canvas.ResetMatrix();

        //using (var paint = canvas.GetSolidFillPaint(SKColors.White))
        //{
        //    var bbox = vp.WorldToViewport(GetBoundingBox());
        //    canvas.DrawRect(bbox.Left, bbox.Top, bbox.Width, bbox.Height, paint);

        //    paint.IsStroke = true;
        //    paint.Color = SKColors.Green;
        //    canvas.DrawRect(bbox.Left, bbox.Top, bbox.Width, bbox.Height, paint);
        //}
        //canvas.Restore();
        //DrawBoundingBox(canvas, vp, 2, SKColors.Cyan);

        //            if(IsPointerOver)
        //                DrawHitZone(canvas, vp, 2, SKColors.Green);
        //            else
        //            {
        //                DrawHitZone(canvas, vp, 2, SKColors.Red);
        //            }

        var hz = GetHitZone();

        var w = Size.Width;
        var h = Size.Height;

        var paint = new SKPaint();
        paint.Color = SKColors.White;

        canvas.Save();

        var transform = vp.ResultTransformMatrix;
        canvas.SetMatrix(transform);
        canvas.DrawCircle(hz.MidX, hz.MidY, vp.PixelsToWorld(w / 2), paint);
        paint.IsStroke = true;
        paint.IsAntialias = true;
        paint.StrokeWidth = vp.PixelsToWorld(2);
        paint.Color = StrokeColor;
        canvas.DrawCircle(hz.MidX, hz.MidY, vp.PixelsToWorld(w / 2), paint);

        DrawBoundingBox(canvas, vp, 2, BBoxColor);

        canvas.Restore();
        //DrawBoundingBox(canvas, vp, 2, StrokeColor);

    }

    public void OnViewChanged(ViewPort vp)
    {
        _actualSize = new SKSize(vp.PixelsToWorld(Size.Width), vp.PixelsToWorld(Size.Height));
        ProjectionTransform = SKMatrix.CreateScale(1 / vp.DpiEffectiveZoom, 1 / vp.DpiEffectiveZoom, PivotPosition.X, PivotPosition.Y);
    }
}