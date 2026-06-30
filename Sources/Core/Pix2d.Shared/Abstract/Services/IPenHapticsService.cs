#nullable enable
namespace Pix2d.Abstract.Services;

/// <summary>
/// Which inking "feel" to request from a haptic-capable pen (e.g. Surface Slim Pen 2). Maps onto the
/// Windows <c>KnownSimpleHapticsControllerWaveforms</c> inking waveforms. <see cref="None"/> means
/// "no haptic for this action" (e.g. a non-freehand tool), so the service plays nothing.
/// </summary>
public enum PenHapticTool
{
    None = 0,
    Pen,    // generic ink — InkContinuous (the guaranteed fallback)
    Pencil, // PencilContinuous
    Marker, // MarkerContinuous
    Brush,  // BrushContinuous
    Eraser, // EraserContinuous
}

/// <summary>
/// Plays continuous "pen on paper" haptic feedback on a haptic-capable stylus while the user draws.
/// The continuous inking waveform is sent on stroke start and the pen firmware vibrates while the tip
/// is in contact, until <see cref="EndStroke"/> (or the tip lifts).
///
/// This is a no-op on every platform/device without pen haptics — that is, everything except
/// Windows 11 (build 22000+) paired with a haptic pen such as the Surface Slim Pen 2. The real
/// implementation lives in the desktop head (WinRT <c>SimpleHapticsController</c>); other heads use
/// <c>NullPenHapticsService</c>.
/// </summary>
public interface IPenHapticsService
{
    /// <summary>
    /// Bind to the top-level native window (the HWND on Windows) so the service can observe pen input
    /// and resolve the active pen device. Pass <c>0</c> / an unknown handle to no-op.
    /// </summary>
    void Attach(nint windowHandle);

    /// <summary>Unbind from the window and stop any active feedback. Safe to call when not attached.</summary>
    void Detach();

    /// <summary>
    /// Pen tip went down to begin a drawing stroke — start the continuous inking waveform for
    /// <paramref name="tool"/>. No-op for <see cref="PenHapticTool.None"/> or a non-haptic pen.
    /// </summary>
    void BeginStroke(PenHapticTool tool);

    /// <summary>Pen lifted / stroke ended — stop the inking waveform. Idempotent; safe when no stroke is active.</summary>
    void EndStroke();
}
