using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// Tracks a touch-down that may become a marquee selection once the pointer moves far enough
/// in viewport space. This lets pinch/pan gestures cancel the pending selection before it starts.
/// </summary>
internal sealed class DeferredTouchSelection
{
    private const float DragThresholdViewportPixels = 12f;
    private SKPoint _startViewportPosition;

    public bool HasPendingSelectionStart { get; private set; }

    public void Begin(SKPoint viewportPosition)
    {
        HasPendingSelectionStart = true;
        _startViewportPosition = viewportPosition;
    }

    public void Cancel()
    {
        HasPendingSelectionStart = false;
    }

    public bool ConsumeTapRelease()
    {
        if (!HasPendingSelectionStart)
            return false;

        HasPendingSelectionStart = false;
        return true;
    }

    public bool TryPromote(SKPoint viewportPosition)
    {
        if (!HasPendingSelectionStart)
            return false;

        var dx = viewportPosition.X - _startViewportPosition.X;
        var dy = viewportPosition.Y - _startViewportPosition.Y;
        if (dx * dx + dy * dy < DragThresholdViewportPixels * DragThresholdViewportPixels)
            return false;

        HasPendingSelectionStart = false;
        return true;
    }
}