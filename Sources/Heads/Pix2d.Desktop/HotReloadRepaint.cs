#if DEBUG
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Pix2d.Desktop;

/// <summary>
/// Works around an Avalonia hot-reload quirk: after <see cref="HotReloadManager"/> rebuilds a view,
/// only that view invalidates itself, and <c>InvalidateVisual</c> is NOT recursive — so descendant
/// visuals keep their cached composition and the window looks half-updated until you resize it
/// (a resize forces a full measure/arrange + re-render of the whole top-level). We reproduce that
/// resize effect programmatically on every hot reload by invalidating the entire visual tree of every
/// open window. DEBUG-only; hot reload itself is Debug-only. Wired from <see cref="Program.OnAppInitialized"/>.
/// </summary>
internal static class HotReloadRepaint
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;
        HotReloadManager.HotReloaded += OnHotReloaded;
    }

    private static void OnHotReloaded(Type[]? updatedTypes)
    {
        // The reload has just rebuilt the affected views synchronously. Post the full-tree refresh so
        // it runs after the current dispatcher message (and the view's own invalidations) settles.
        Dispatcher.UIThread.Post(RefreshAllWindows, DispatcherPriority.Background);
    }

    private static void RefreshAllWindows()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        foreach (var window in desktop.Windows.ToArray())
        {
            try
            {
                ForceFullRedraw(window);
            }
            catch (Exception ex)
            {
                // A hot-reload cosmetic fix must never take the app down.
                System.Diagnostics.Debug.WriteLine($"[HotReloadRepaint] {window.GetType().Name}: {ex}");
            }
        }
    }

    private static void ForceFullRedraw(Window window)
    {
        // Invalidating measure/arrange schedules a full layout pass, and invalidating every visual
        // re-records the whole compositor surface — together the same net effect as a manual resize.
        foreach (var visual in window.GetSelfAndVisualDescendants())
        {
            if (visual is Layoutable layoutable)
            {
                layoutable.InvalidateMeasure();
                layoutable.InvalidateArrange();
            }

            visual.InvalidateVisual();
        }
    }
}
#endif
