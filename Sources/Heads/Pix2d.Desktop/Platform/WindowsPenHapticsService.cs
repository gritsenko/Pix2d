#nullable enable
#if WINDOWS
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Pix2d.Abstract.Services;
using Windows.Devices.Haptics;
using Windows.Devices.Input;

namespace Pix2d.Desktop.Platform;

/// <summary>
/// Windows 11 implementation of <see cref="IPenHapticsService"/> using the WinRT
/// <see cref="SimpleHapticsController"/> exposed by a haptic-capable pen (Surface Slim Pen 2 etc.).
///
/// Avalonia hides the OS pointer id behind its own counter, but <see cref="PenDevice.GetFromPointerId"/>
/// needs the real Windows pointer id (from <c>WM_POINTER*</c>). So we install a lightweight window
/// subclass that only sniffs pen <c>WM_POINTER</c> messages and remembers the current pen pointer id —
/// it never consumes input (every message is forwarded via <c>DefSubclassProc</c>). With that id we can
/// resolve the pen's haptics controller on stroke start.
///
/// Everything degrades to a silent no-op: a non-haptic pen yields a null controller, and the
/// <c>22000</c> version guard keeps the Win11-only API off Windows 10 (where the app still runs fine).
/// </summary>
public sealed class WindowsPenHapticsService : IPenHapticsService
{
    // WM_POINTER* messages we sniff to learn the active pen's pointer id.
    private const uint WM_POINTERUPDATE = 0x0245;
    private const uint WM_POINTERDOWN = 0x0246;
    private const uint WM_POINTERENTER = 0x0249;

    private const uint PT_PEN = 3; // POINTER_INPUT_TYPE.PT_PEN
    private const nuint SubclassId = 1;

    private nint _hwnd;
    private SubclassProc? _subclassProc; // kept rooted so the native callback delegate isn't collected
    private bool _subclassed;

    private uint _currentPenPointerId;
    private bool _hasPenId;

    private SimpleHapticsController? _controller;
    private bool _inkingActive;

    public void Attach(nint windowHandle)
    {
        if (windowHandle == 0)
            return;

        if (_subclassed && _hwnd == windowHandle)
            return;

        Detach();

        _hwnd = windowHandle;
        _subclassProc = OnSubclass;
        try
        {
            _subclassed = SetWindowSubclass(_hwnd, _subclassProc, SubclassId, 0);
        }
        catch (Exception ex)
        {
            // comctl32 missing / blocked — feature stays off, app unaffected.
            Logger.LogException(ex);
            _subclassed = false;
        }
    }

    public void Detach()
    {
        StopInkingInternal();

        if (_subclassed && _hwnd != 0 && _subclassProc != null)
        {
            try { RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId); }
            catch { /* tearing down — ignore */ }
        }

        _subclassed = false;
        _hwnd = 0;
        _subclassProc = null;
        _controller = null;
        _hasPenId = false;
    }

    public void BeginStroke(PenHapticTool tool)
    {
        if (tool == PenHapticTool.None)
            return;

        // PenDevice.SimpleHapticsController is a Windows 11 (10.0.22000) API. On older Windows the app
        // runs as before; we simply never enter this branch.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        var controller = _controller ??= AcquirePenController();
        if (controller == null)
            return;

        var waveform = PickWaveform(controller, tool);
        if (waveform == null)
            return;

        try
        {
            controller.SendHapticFeedback(waveform);
            _inkingActive = true;
        }
        catch (Exception ex)
        {
            // Pen left range / dropped — drop the cached controller so the next stroke re-resolves it.
            _controller = null;
            _inkingActive = false;
            Logger.LogException(ex);
        }
    }

    public void EndStroke() => StopInkingInternal();

    private void StopInkingInternal()
    {
        if (!_inkingActive)
            return;

        _inkingActive = false;
        try { _controller?.StopFeedback(); }
        catch { /* nothing useful to do if stop fails */ }
    }

    [SupportedOSPlatform("windows10.0.22000")]
    private SimpleHapticsController? AcquirePenController()
    {
        if (!_hasPenId)
            return null;

        try
        {
            var pen = PenDevice.GetFromPointerId(_currentPenPointerId);
            return pen?.SimpleHapticsController;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return null;
        }
    }

    /// <summary>
    /// Pick the requested inking waveform, falling back to <c>InkContinuous</c> (guaranteed supported by
    /// any haptic pen) when the tool-specific one is unavailable.
    /// </summary>
    private static SimpleHapticsControllerFeedback? PickWaveform(SimpleHapticsController controller, PenHapticTool tool)
    {
        var desired = tool switch
        {
            PenHapticTool.Eraser => KnownSimpleHapticsControllerWaveforms.EraserContinuous,
            PenHapticTool.Marker => KnownSimpleHapticsControllerWaveforms.MarkerContinuous,
            PenHapticTool.Pencil => KnownSimpleHapticsControllerWaveforms.PencilContinuous,
            PenHapticTool.Brush => KnownSimpleHapticsControllerWaveforms.BrushContinuous,
            _ => KnownSimpleHapticsControllerWaveforms.InkContinuous,
        };

        SimpleHapticsControllerFeedback? match = null;
        SimpleHapticsControllerFeedback? inkFallback = null;

        foreach (var feedback in controller.SupportedFeedback)
        {
            if (feedback.Waveform == desired)
            {
                match = feedback;
                break;
            }

            if (feedback.Waveform == KnownSimpleHapticsControllerWaveforms.InkContinuous)
                inkFallback = feedback;
        }

        return match ?? inkFallback;
    }

    private nint OnSubclass(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        switch (uMsg)
        {
            case WM_POINTERENTER:
            case WM_POINTERDOWN:
            case WM_POINTERUPDATE:
                // GET_POINTERID_WPARAM(wParam) == LOWORD(wParam)
                var pointerId = (uint)((long)wParam & 0xFFFF);
                try
                {
                    if (GetPointerType(pointerId, out var type) && type == PT_PEN)
                    {
                        _currentPenPointerId = pointerId;
                        _hasPenId = true;
                    }
                }
                catch
                {
                    // never throw out of the window procedure — it would break the input pump
                }
                break;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // SUBCLASSPROC: LRESULT (HWND, UINT, WPARAM, LPARAM, UINT_PTR uIdSubclass, DWORD_PTR dwRefData)
    private delegate nint SubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerType(uint pointerId, out uint pointerType);
}
#endif
