namespace Pix2d.Primitives.ViewPort;

/// <summary>
/// Decides whether the scroll events arriving right now come from a precision device (a trackpad, or a
/// touchpad-style scroll surface) rather than a notched mouse wheel. The platform does not tell us: a
/// two-finger trackpad scroll reaches Avalonia as an ordinary <c>PointerWheelChanged</c> with
/// <c>PointerType.Mouse</c>, and nothing in <c>PointerWheelEventArgs</c> distinguishes the two.
///
/// The verdict is a <b>latch</b>: once a precision device is recognized it stays recognized until a real
/// wheel notch shows up, because a single event is far too little evidence. It exists so the
/// "mouse wheel behavior" setting can apply to a wheel only — a trackpad's zoom affordance is the pinch
/// gesture (or Ctrl+scroll), so its two-finger scroll should always pan.
/// </summary>
public sealed class PrecisionScrollDetector
{
    /// <summary>
    /// A whole-step single-axis delta only counts as a wheel notch when it arrives after this long a gap.
    /// Inside a burst the current verdict stands: a precision stream can land on a whole number now and
    /// then, but no hand can notch a wheel this fast.
    /// </summary>
    private const ulong WheelNotchGapMilliseconds = 200;

    private const double WholeStepTolerance = 0.001;

    private ulong _lastEventTimestamp;

    /// <summary>True while the latch says the current scroll source is a precision device.</summary>
    public bool IsPrecisionScrolling { get; private set; }

    /// <summary>
    /// Feeds one scroll event (delta plus its event timestamp in milliseconds) and returns the current
    /// verdict.
    /// </summary>
    public bool Observe(double deltaX, double deltaY, ulong timestampMilliseconds)
    {
        var gap = timestampMilliseconds >= _lastEventTimestamp
            ? timestampMilliseconds - _lastEventTimestamp
            : ulong.MaxValue; // clock went backwards — treat as "long ago" rather than wrapping around
        _lastEventTimestamp = timestampMilliseconds;

        // Moving both axes within one event is the one signal a wheel cannot fake: a notched wheel scrolls
        // a single axis at a time (a tilt wheel never coincides with a vertical notch), while fingers on a
        // surface always drift diagonally. Fractional deltas deliberately do NOT count as evidence —
        // high-resolution mice (Logitech SmartShift and friends) emit sub-notch fractions too, and reading
        // one of those as a trackpad would silently override the user's "wheel = zoom" setting.
        if (deltaX != 0 && deltaY != 0)
        {
            IsPrecisionScrolling = true;
            return IsPrecisionScrolling;
        }

        if (IsWheelNotch(deltaX, deltaY) && gap > WheelNotchGapMilliseconds)
            IsPrecisionScrolling = false;

        return IsPrecisionScrolling;
    }

    /// <summary>
    /// Reports a platform touchpad gesture (pinch / rotate / swipe). Those events are only ever synthesized
    /// from a touchpad, so they are proof — and they also cover the case of a user whose two-finger scrolls
    /// happen to stay perfectly vertical.
    /// </summary>
    public void NotifyTouchPadGesture() => IsPrecisionScrolling = true;

    /// <summary>A whole step on one axis, i.e. what a notched wheel reports (Windows' WHEEL_DELTA → ±1).</summary>
    private static bool IsWheelNotch(double deltaX, double deltaY)
        => deltaX == 0 && Math.Abs(deltaY) >= 1 && Math.Abs(deltaY - Math.Round(deltaY)) < WholeStepTolerance;
}
