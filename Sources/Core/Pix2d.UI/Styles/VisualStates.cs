using Avalonia.Styling;

namespace Pix2d.UI.Styles;

public static class VisualStates
{
    public static Selector Wide() => ((Selector)null!).Class(nameof(Wide)).Descendant();
    public static Selector Narrow() => ((Selector)null!).Class(nameof(Narrow)).Descendant();
}