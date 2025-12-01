using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Pix2d;

public class MultiFingerGestureRecognizer : GestureRecognizer
{
    private int _fingersCount = 2;
    private int _tapCount = 1;
    private int _currentTapCount = 0;
    private int _maxFingersDown = 0;
    
    private readonly Dictionary<int, Point> _pointers = new();
    
    private DispatcherTimer? _doubleTapTimer;
    
    private const int TapDurationMs = 800;
    private const int DoubleTapIntervalMs = 800;
    private const double MaxMovement = 50;

    private long _startTime;
    private bool _tracking = false;

    public int FingersCount
    {
        get => _fingersCount;
        set => _fingersCount = value;
    }

    public int TapCount
    {
        get => _tapCount;
        set => _tapCount = value;
    }

    public RoutedEvent? RoutedEventToRaise { get; set; }

    public event EventHandler? Recognized;

    protected override void PointerPressed(PointerPressedEventArgs e)
    {
        if (Target == null || !(Target is Visual target)) return;

        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] PointerPressed: Id={e.Pointer.Id}, Type={e.Pointer.Type}");

        if (!_tracking)
        {
            _tracking = true;
            _startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            _maxFingersDown = 0;
            _pointers.Clear();
            Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Started tracking");
        }
        
        if (!_pointers.ContainsKey(e.Pointer.Id))
        {
            _pointers.Add(e.Pointer.Id, e.GetPosition(target));
            Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Added pointer {e.Pointer.Id}, total pointers: {_pointers.Count}");
        }
        
        _maxFingersDown = Math.Max(_maxFingersDown, _pointers.Count);
        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] MaxFingersDown={_maxFingersDown}");
    }

    protected override void PointerMoved(PointerEventArgs e)
    {
        if (Target == null || !(Target is Visual target)) return;

        if (_tracking && _pointers.TryGetValue(e.Pointer.Id, out var startPoint))
        {
            var pos = e.GetPosition(target);
            var dist = Math.Sqrt(Math.Pow(pos.X - startPoint.X, 2) + Math.Pow(pos.Y - startPoint.Y, 2));
            if (dist > MaxMovement)
            {
                Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Cancelled due to movement: {dist:F1}px > {MaxMovement}px");
                Cancel();
            }
        }
    }

    protected override void PointerReleased(PointerReleasedEventArgs e)
    {
        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] PointerReleased: Id={e.Pointer.Id}, tracking={_tracking}, containsKey={_pointers.ContainsKey(e.Pointer.Id)}");
        
        if (_tracking && _pointers.ContainsKey(e.Pointer.Id))
        {
            _pointers.Remove(e.Pointer.Id);
            Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Removed pointer, remaining: {_pointers.Count}");
            
            if (_pointers.Count == 0)
            {
                // All fingers released.
                long duration = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _startTime;
                Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] All released. Duration={duration}ms, MaxFingers={_maxFingersDown}, Expected={FingersCount}");
                
                if (_maxFingersDown == FingersCount && duration < TapDurationMs)
                {
                    // Valid tap!
                    _currentTapCount++;
                    Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Valid tap! TapCount={_currentTapCount}/{TapCount}");
                    
                    if (_currentTapCount == TapCount)
                    {
                        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] *** GESTURE RECOGNIZED! ***");
                        Recognized?.Invoke(this, EventArgs.Empty);
                        if (RoutedEventToRaise != null && Target is Interactive interactive)
                        {
                            Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Raising routed event");
                            interactive.RaiseEvent(new RoutedEventArgs(RoutedEventToRaise));
                        }
                        _currentTapCount = 0;
                        _doubleTapTimer?.Stop();
                    }
                    else
                    {
                        // Start timer for double tap
                        if (_doubleTapTimer == null)
                        {
                            _doubleTapTimer = new DispatcherTimer();
                            _doubleTapTimer.Interval = TimeSpan.FromMilliseconds(DoubleTapIntervalMs);
                            _doubleTapTimer.Tick += (s, args) => 
                            {
                                Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Double tap timer expired, resetting");
                                _currentTapCount = 0;
                                _doubleTapTimer.Stop();
                            };
                        }
                        _doubleTapTimer.Stop();
                        _doubleTapTimer.Start();
                        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Waiting for next tap...");
                    }
                    _tracking = false; // Ready for next tap
                }
                else
                {
                    Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Invalid tap - wrong finger count or too slow");
                    Cancel();
                }
            }
        }
    }

    protected override void PointerCaptureLost(IPointer pointer)
    {
        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] PointerCaptureLost: Id={pointer.Id}");
        // Treat capture lost as a pointer release
        if (_tracking && _pointers.ContainsKey(pointer.Id))
        {
            _pointers.Remove(pointer.Id);
            Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Removed pointer on capture lost, remaining: {_pointers.Count}");
            
            if (_pointers.Count == 0)
            {
                // All fingers released (via capture lost)
                long duration = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _startTime;
                Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] All released (capture lost). Duration={duration}ms, MaxFingers={_maxFingersDown}, Expected={FingersCount}");
                
                if (_maxFingersDown == FingersCount && duration < TapDurationMs)
                {
                    // Valid tap!
                    _currentTapCount++;
                    Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Valid tap (capture lost)! TapCount={_currentTapCount}/{TapCount}");
                    
                    if (_currentTapCount == TapCount)
                    {
                        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] *** GESTURE RECOGNIZED (capture lost)! ***");
                        Recognized?.Invoke(this, EventArgs.Empty);
                        if (RoutedEventToRaise != null && Target is Interactive interactive)
                        {
                            Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Raising routed event");
                            interactive.RaiseEvent(new RoutedEventArgs(RoutedEventToRaise));
                        }
                        _currentTapCount = 0;
                        _doubleTapTimer?.Stop();
                    }
                    else
                    {
                        // Start timer for double tap
                        if (_doubleTapTimer == null)
                        {
                            _doubleTapTimer = new DispatcherTimer();
                            _doubleTapTimer.Interval = TimeSpan.FromMilliseconds(DoubleTapIntervalMs);
                            _doubleTapTimer.Tick += (s, args) => 
                            {
                                Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Double tap timer expired, resetting");
                                _currentTapCount = 0;
                                _doubleTapTimer.Stop();
                            };
                        }
                        _doubleTapTimer.Stop();
                        _doubleTapTimer.Start();
                        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Waiting for next tap...");
                    }
                    _tracking = false; // Ready for next tap
                }
                else
                {
                    Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Invalid tap (capture lost) - wrong finger count or too slow");
                    Cancel();
                }
            }
        }
    }

    private void Cancel()
    {
        Debug.WriteLine($"[MultiFingerGesture-{FingersCount}] Cancel() called");
        _tracking = false;
        _pointers.Clear();
        _maxFingersDown = 0;
        _currentTapCount = 0;
        _doubleTapTimer?.Stop();
    }
}
