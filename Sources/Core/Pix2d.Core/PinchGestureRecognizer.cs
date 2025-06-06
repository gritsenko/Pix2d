﻿using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;

namespace Pix2d;

public class PinchGestureRecognizer : GestureRecognizer
{
    private IInputElement? _target;
    private IGestureRecognizerActionsDispatcher? _actions;
    private float _initialDistance;
    private IPointer? _firstContact;
    private Point _firstPoint;
    private IPointer? _secondContact;
    private Point _secondPoint;
    private Point _origin;
    private Point _originOffset;

    public Point Offset => _originOffset;
    public void Initialize(IInputElement target, IGestureRecognizerActionsDispatcher actions)
    {
        _target = target;
        _actions = actions;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPressed(e);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        PointerReleased(e);
    }

    public void PointerCaptureLost(IPointer pointer)
    {
        RemoveContact(pointer);
    }

    public void PointerMoved(PointerEventArgs e)
    {
        if (_target != null && _target is Visual visual)
        {
            if (_firstContact == e.Pointer)
            {
                _firstPoint = e.GetPosition(visual);
            }
            else if (_secondContact == e.Pointer)
            {
                _secondPoint = e.GetPosition(visual);
            }
            else
            {
                return;
            }

            if (_firstContact != null && _secondContact != null)
            {
                var distance = GetDistance(_firstPoint, _secondPoint);
                _originOffset = new Point((_firstPoint.X + _secondPoint.X) / 2.0f, (_firstPoint.Y + _secondPoint.Y) / 2.0f);

                var scale = distance / _initialDistance;

                _target?.RaiseEvent(new PinchEventArgs(scale, _origin));
            }
        }
    }

    public void PointerPressed(PointerPressedEventArgs e)
    {
        if (_target != null && _target is Visual visual && (e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen))
        {
            if (_firstContact == null)
            {
                _firstContact = e.Pointer;
                _firstPoint = e.GetPosition(visual);

                return;
            }
            else if (_secondContact == null && _firstContact != e.Pointer)
            {
                _secondContact = e.Pointer;
                _secondPoint = e.GetPosition(visual);
            }
            else
            {
                return;
            }

            if (_firstContact != null && _secondContact != null)
            {
                _initialDistance = GetDistance(_firstPoint, _secondPoint);

                _origin = new Point((_firstPoint.X + _secondPoint.X) / 2.0f, (_firstPoint.Y + _secondPoint.Y) / 2.0f);

                _actions!.Capture(_firstContact, this);
                _actions!.Capture(_secondContact, this);
            }
        }
    }

    public void PointerReleased(PointerReleasedEventArgs e)
    {
        RemoveContact(e.Pointer);
    }

    private void RemoveContact(IPointer pointer)
    {
        if (_firstContact == pointer || _secondContact == pointer)
        {
            if (_secondContact == pointer)
            {
                _secondContact = null;
            }

            if (_firstContact == pointer)
            {
                _firstContact = _secondContact;

                _secondContact = null;
            }
            _target?.RaiseEvent(new PinchEndedEventArgs());
        }
    }

    private float GetDistance(Point a, Point b)
    {
        var length = _secondPoint - _firstPoint;
        return (float)new Vector(length.X, length.Y).Length;
    }
}
