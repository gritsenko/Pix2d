using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Pix2d.Desktop.Services;

internal static class TouchHelper
{
    private const string TabletPenServiceProperty = "MicrosoftTabletPenServiceProperty";
    private const int WM_TABLET_QUERYSYSTEMGESTURESTATUS = 0x02CC;
    private const int WM_NCDESTROY = 0x0082;
    private const uint TouchHelperSubclassId = 1;
    private const uint TABLET_DISABLE_PRESSANDHOLD = 0x00000001;
    private const uint TABLET_DISABLE_PENTAPFEEDBACK = 0x00000008;
    private const uint TABLET_DISABLE_PENBARRELFEEDBACK = 0x00000010;
    private const uint TABLET_DISABLE_TOUCHUIFORCEOFF = 0x00000200;
    private const uint TABLET_DISABLE_TOUCHSWITCH = 0x00008000;
    private const uint TABLET_DISABLE_FLICKS = 0x00010000;
    private const uint TABLET_DISABLE_SMOOTHSCROLLING = 0x00080000;
    private const uint TABLET_DISABLE_FLICKFALLBACKKEYS = 0x00100000;
    private const uint TABLET_ENABLE_MULTITOUCHDATA = 0x01000000;
    private static readonly IntPtr TabletGestureFlags = new(
        TABLET_DISABLE_PRESSANDHOLD |
        TABLET_DISABLE_PENTAPFEEDBACK |
        TABLET_DISABLE_PENBARRELFEEDBACK |
        TABLET_DISABLE_TOUCHUIFORCEOFF |
        TABLET_DISABLE_TOUCHSWITCH |
        TABLET_DISABLE_FLICKS |
        TABLET_DISABLE_SMOOTHSCROLLING |
        TABLET_DISABLE_FLICKFALLBACKKEYS |
        TABLET_ENABLE_MULTITOUCHDATA);
    private static readonly HashSet<IntPtr> ConfiguredWindows = [];
    private static readonly SubclassProc TouchWindowProc = HandleTouchWindowMessage;
    private static readonly FeedbackType[] DisabledFeedbackTypes =
    [
        FeedbackType.TouchContactVisualization,
        FeedbackType.TouchTap,
        FeedbackType.TouchDoubleTap,
        FeedbackType.TouchPressAndHold,
        FeedbackType.TouchRightTap,
        FeedbackType.GesturePressAndTap
    ];

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowFeedbackSetting(IntPtr hwnd, FeedbackType feedback, uint dwFlags, uint size, ref int configuration);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData);

    public static void ConfigureTouchHandling(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        window.Opened -= OnWindowOpened;
        window.Opened += OnWindowOpened;
        TryConfigureWindow(window);
    }

    private static void OnWindowOpened(object? sender, EventArgs args)
    {
        if (sender is not Window window)
        {
            return;
        }

        TryConfigureWindow(window);
    }

    private static void TryConfigureWindow(Window window)
    {
        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
        {
            return;
        }

        if (!ConfiguredWindows.Add(hwnd))
        {
            return;
        }

        SetProp(hwnd, TabletPenServiceProperty, TabletGestureFlags);
        DisableWindowFeedback(hwnd);
        SetWindowSubclass(hwnd, TouchWindowProc, TouchHelperSubclassId, IntPtr.Zero);
    }

    private static void DisableWindowFeedback(IntPtr hwnd)
    {
        int disabled = 0;
        foreach (var feedbackType in DisabledFeedbackTypes)
        {
            SetWindowFeedbackSetting(hwnd, feedbackType, 0, sizeof(int), ref disabled);
        }
    }

    private static IntPtr HandleTouchWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
    {
        if (msg == WM_TABLET_QUERYSYSTEMGESTURESTATUS)
        {
            return TabletGestureFlags;
        }

        if (msg == WM_NCDESTROY)
        {
            ConfiguredWindows.Remove(hWnd);
            RemoveWindowSubclass(hWnd, TouchWindowProc, uIdSubclass);
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private enum FeedbackType : uint
    {
        TouchContactVisualization = 1,
        TouchTap = 7,
        TouchDoubleTap = 8,
        TouchPressAndHold = 9,
        TouchRightTap = 10,
        GesturePressAndTap = 11
    }
}