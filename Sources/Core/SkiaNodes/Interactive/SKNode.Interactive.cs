using System;
using System.Diagnostics;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace SkiaNodes;

public partial class SKNode : IInteractive
{
    protected SKPoint StartPos;
    protected SKPoint EndPos;
    protected SKPoint DistanceToPointer;

    public event EventHandler Clicked;
    public event EventHandler<PointerActionEventArgs> DoubleClicked;
    public event EventHandler<PointerActionEventArgs> PointerPressed;
    public event EventHandler<PointerActionEventArgs> PointerReleased;
    public event EventHandler<PointerActionEventArgs> PointerMoved;
    public event EventHandler PointerEntered;
    public event EventHandler PointerExited;

    private int _distanceMoved = 0;

    public virtual void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
            StartPos = eventArgs.Pointer.WorldPosition;
            EndPos = StartPos;
            DistanceToPointer = new SKPoint();

            _distanceMoved = 0;

            if (clickCount == 2)
                OnPointerDoublePressed(eventArgs);

            PointerPressed?.Invoke(this, eventArgs);
        }

    public virtual void OnPointerDoublePressed(PointerActionEventArgs eventArgs)
    {
            var args = new PointerActionEventArgs(PointerActionType.DoublePressed, eventArgs.Pointer, eventArgs.KeyModifiers);
            DoubleClicked?.Invoke(this, args);
        }

    public void OnPointerReleasedCore(PointerActionEventArgs eventArgs)
    {
            EndPos = eventArgs.Pointer.WorldPosition;

            if (_distanceMoved < 2)
            {
                OnClicked();
            }

            OnPointerReleased(eventArgs);

            PointerReleased?.Invoke(this, eventArgs);
        }

    public virtual void OnPointerReleased(PointerActionEventArgs eventArgs)
    {
        }

    public virtual void CapturePointer()
    {
            Debug.WriteLine("Captured by " +this.GetType().Name);
            SKInput.Current.CapturePointer(this);
        }

    public void ReleasePointerCapture()
    {
            Debug.WriteLine("Released by " + this.GetType().Name);
            SKInput.Current.ReleasePointer(this);
        }

    public virtual void OnPointerMoved(PointerActionEventArgs eventArgs)
    {
            EndPos = eventArgs.Pointer.WorldPosition;
            _distanceMoved++;

            PointerMoved?.Invoke(this, eventArgs);
        }

    public virtual void OnPointerEnter(SKPoint pos)
    {
            PointerEntered?.Invoke(this, EventArgs.Empty);
        }

    public virtual void OnPointerLeave(SKPoint pos)
    {
            PointerExited?.Invoke(this, EventArgs.Empty);
        }

    public virtual void OnPanModeChanged(bool isPanModeEnabled)
    {
            
        }


    protected virtual void OnClicked()
    {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
}