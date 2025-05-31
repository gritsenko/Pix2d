using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs;

public class ThumbNode : SKNode
{
    protected bool IsPointerOver;
    public const double MinDragLength = 0;

    public event EventHandler<DragCompletedEventArgs> DragComplete;
    public event EventHandler<DragDeltaEventArgs> DragDelta;
    public event EventHandler<DragStartedEventArgs> DragStarted;

    public ThumbDirection Direction { get; set; }

    public bool UseSnapping { get; set; }

    public bool IsDragging { get; set; }

    public ThumbNode()
    {
        IsInteractive = true;
    }

    public override void OnPointerMoved(PointerActionEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);

        if (eventArgs.Pointer.IsPressed && SKInput.Current.CapturedPointerBy == this)
        {
            var dragDelta = ProcessDirection(EndPos - StartPos);

            if (dragDelta.GetVectorLength() > MinDragLength)
            {
                DragDelta?.Invoke(this, new DragDeltaEventArgs(dragDelta.X, dragDelta.Y));
            }
            eventArgs.Handled = true;
        }
    }

    private SKPoint ProcessDirection(SKPoint delta)
    {
        if (Direction == ThumbDirection.Vertical)
            return new SKPoint(0, delta.Y);
        if (Direction == ThumbDirection.Horizontal)
            return new SKPoint(delta.X, 0);

        return delta;
    }

    public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
        base.OnPointerPressed(eventArgs, clickCount);

        if (clickCount == 1)
        {
            CapturePointer();

            IsDragging = true;

            DragStarted?.Invoke(this,
                new DragStartedEventArgs(eventArgs.Pointer.WorldPosition.X, eventArgs.Pointer.WorldPosition.Y));

        }

        //eventArgs.Handled = true;
    }

    public override void OnPointerReleased(PointerActionEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        var dragDelta = EndPos - StartPos;

        if (dragDelta.GetVectorLength() > MinDragLength && SKInput.Current.CapturedPointerBy == this)
        {
            DragComplete?.Invoke(this, new DragCompletedEventArgs(dragDelta.X, dragDelta.Y, false));
            eventArgs.Handled = true;
        }

        ReleasePointerCapture();
        IsDragging = false;
    }

    public override void OnPointerEnter(SKPoint pos)
    {
        IsPointerOver = true;
        base.OnPointerEnter(pos);
    }

    public override void OnPointerLeave(SKPoint pos)
    {
        IsPointerOver = false;
        base.OnPointerLeave(pos);
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        using var paint = new SKPaint()
        {
            Color = SKColors.Gray,
            IsStroke = false
        };
        canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
    }
}