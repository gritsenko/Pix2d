#nullable enable
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Pix2d.UI.Shared;

public static class ViewportSizeExtensions
{
    /// <summary>
    /// Clamp a control's <see cref="Layoutable.MaxWidth"/> to the current top-level (window) client
    /// width minus <paramref name="horizontalMargin"/>, updating on every resize.
    ///
    /// <para>Use this for a horizontal <c>ScrollViewer</c> that sits in a grid column whose width is
    /// contaminated by other rows' content (e.g. the top tool/action bars span the full width, but the
    /// side Auto columns hold the bottom zoom/layers panels). In that situation neither a star column nor
    /// the natural measure gives the scroll host a viewport-bounded width, so it sizes to its content and
    /// overflows instead of scrolling. Binding MaxWidth straight to the window client size sidesteps the
    /// grid entirely.</para>
    /// </summary>
    public static T ClampMaxWidthToViewport<T>(this T control, double horizontalMargin = 0) where T : Control
    {
        IDisposable? sub = null;

        control.AttachedToVisualTree += (_, _) =>
        {
            Visual? top = TopLevel.GetTopLevel(control);
            if (top == null)
                return;

            void Apply(Rect bounds) => control.MaxWidth = Math.Max(44, bounds.Width - horizontalMargin);

            Apply(top.Bounds);
            sub?.Dispose();
            sub = top.GetObservable(Visual.BoundsProperty)
                .Subscribe(new AnonymousObserver<Rect>(Apply));
        };

        control.DetachedFromVisualTree += (_, _) =>
        {
            sub?.Dispose();
            sub = null;
        };

        return control;
    }
}
