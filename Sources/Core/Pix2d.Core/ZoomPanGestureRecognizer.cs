#nullable enable
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;

namespace Pix2d;

public class ZoomPanGestureRecognizer : GestureRecognizer
{
    private float _initialDistance;
    private IPointer? _firstContact;
    private Point _firstPoint;
    private IPointer? _secondContact;
    private Point _secondPoint;
    private Point _origin;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => this.PointerPressed(e);

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => this.PointerReleased(e);

    protected override void PointerCaptureLost(IPointer pointer) => this.RemoveAllContacts();

    private void RemoveAllContacts()
    {
        this._firstContact = null;
        this._secondContact = null;
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (this.Target == null || !(this.Target is Visual target))
            return;
        
        if (this._firstContact == e.Pointer)
        {
            this._firstPoint = e.GetPosition(target);
        }
        else
        {
            if (this._secondContact != e.Pointer)
                return;
            
            this._secondPoint = e.GetPosition(target);
        }

        if (this._firstContact == null || this._secondContact == null)
            return;
        
        var origin = new Point((this._firstPoint.X + this._secondPoint.X) / 2.0,
            (this._firstPoint.Y + this._secondPoint.Y) / 2.0);
        
        PinchEventArgs e1 =
            new PinchEventArgs(
                (double) this.GetDistance(this._firstPoint, this._secondPoint) / (double) this._initialDistance,
                origin);
        this.Target?.RaiseEvent((RoutedEventArgs) e1);
        e.Handled = e1.Handled;
    }

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (this.Target == null || !(this.Target is Visual target) ||
            e.Pointer.Type != PointerType.Touch && e.Pointer.Type != PointerType.Pen)
            return;
        
        if (this._firstContact == null)
        {
            this._firstContact = e.Pointer;
            this._firstPoint = e.GetPosition(target);
        }
        else
        {
            if (this._secondContact != null || this._firstContact == e.Pointer)
            {
                return;
            }
            
            this._secondContact = e.Pointer;
            this._secondPoint = e.GetPosition(target);
            if (this._firstContact == null || this._secondContact == null)
                return;
            
            this._initialDistance = this.GetDistance(this._firstPoint, this._secondPoint);
            this._origin = new Point((this._firstPoint.X + this._secondPoint.X) / 2.0,
                (this._firstPoint.Y + this._secondPoint.Y) / 2.0);
            this.Capture(this._firstContact);
            this.Capture(this._secondContact);
        }
    }

    protected override void PointerReleased(PointerReleasedEventArgs e) => this.RemoveContact(e.Pointer);

    private void RemoveContact(IPointer pointer)
    {
        if (this._firstContact != pointer && this._secondContact != pointer)
        {
            return;
        }

        if (this._secondContact == pointer)
        {
            this._secondContact = null;
        }
        if (this._firstContact == pointer)
        {
            this._firstContact = this._secondContact;
            this._secondContact = null;
        }

        this.Target?.RaiseEvent((RoutedEventArgs) new PinchEndedEventArgs());
    }

    private float GetDistance(Point a, Point b)
    {
        Point point = this._secondPoint - this._firstPoint;
        return (float) new Vector(point.X, point.Y).Length;
    }
}