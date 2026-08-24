using Avalonia.Threading;

namespace Pix2d.UI.Shared;

/// <summary>
/// Horizontal panel that lays its children out left-to-right and <b>drops the ones that do not
/// fit</b> instead of overflowing, scrolling or wrapping. Written for the narrow-screen project tab
/// strip: the tabs that fit stay on the bar, the rest are reachable through an overflow dropdown.
///
/// A hidden child is not removed and its <c>IsVisible</c> is not touched (writing layout-affecting
/// properties from inside a layout pass invalidates the pass that is running) — it is arranged just
/// past the right edge (so <see cref="Visual.ClipToBounds"/> must be set on the panel to keep it out
/// of sight) and has <c>IsHitTestVisible</c> cleared. The clip alone is not enough: an off-panel
/// child still answers hit-tests, and it would then swallow clicks meant for whatever sits to the
/// right of the panel. The first child is always laid out, clamped to the panel width, so the bar
/// can never end up empty.
///
/// <see cref="VisibleCountChanged"/> is raised on the dispatcher (never inside arrange) whenever the
/// number of laid-out children changes.
/// </summary>
public class OverflowPanel : Panel
{
    /// <summary>Gap between children, in DIP. Not applied after the last visible child.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<OverflowPanel, double>(nameof(Spacing));

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private const double Epsilon = 0.5;

    private int _visibleCount = -1;
    private bool _notificationPending;

    /// <summary>
    /// How many leading children were laid out by the last arrange pass. -1 until the panel has
    /// been arranged once — callers must treat that as "not known yet", not as "nothing fits".
    /// </summary>
    public int VisibleCount => _visibleCount;

    /// <summary>Raised (on the UI thread, after the layout pass) when <see cref="VisibleCount"/> changes.</summary>
    public event EventHandler? VisibleCountChanged;

    static OverflowPanel()
    {
        AffectsMeasure<OverflowPanel>(SpacingProperty);
        AffectsArrange<OverflowPanel>(SpacingProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0d;
        var height = 0d;

        // Every child is measured unconstrained: a child that will be dropped still needs a real
        // DesiredSize, because arrange decides what fits from those widths.
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width + Spacing;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        if (Children.Count > 0)
            width -= Spacing;

        // Never ask for more than we were offered — asking for the full width would push the
        // dropdown button out of the strip instead of dropping a tab.
        if (!double.IsInfinity(availableSize.Width))
            width = Math.Min(width, availableSize.Width);

        return new Size(Math.Max(0, width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0d;
        var visible = 0;
        var dropping = false;

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var width = child.DesiredSize.Width;

            // i == 0: the first tab is never dropped, only clamped — a strip showing nothing at all
            // would leave the user with no visible active project.
            if (!dropping && (i == 0 || x + width <= finalSize.Width + Epsilon))
            {
                child.Arrange(new Rect(x, 0, i == 0 ? Math.Min(width, finalSize.Width) : width, finalSize.Height));
                child.IsHitTestVisible = true;
                x += width + Spacing;
                visible++;
            }
            else
            {
                dropping = true;
                child.Arrange(new Rect(finalSize.Width, 0, width, finalSize.Height));
                // Neither IsHitTestVisible nor the arrange position affects measure/arrange, so
                // this cannot re-enter the layout pass we are inside.
                child.IsHitTestVisible = false;
            }
        }

        if (visible != _visibleCount)
        {
            _visibleCount = visible;
            NotifyVisibleCountChanged();
        }

        return finalSize;
    }

    private void NotifyVisibleCountChanged()
    {
        if (_notificationPending)
            return;

        // Handlers show/hide the dropdown button, i.e. they invalidate layout — post so that
        // happens after the pass we are in, not inside it.
        _notificationPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _notificationPending = false;
            VisibleCountChanged?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Background);
    }
}
