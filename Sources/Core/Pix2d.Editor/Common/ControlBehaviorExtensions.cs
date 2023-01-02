using Avalonia.Xaml.Interactivity;

namespace Pix2d.Common;

public static class ControlBehaviorExtensions
{

    public static TControl AddBehavior<TControl>(this TControl control, IBehavior behavior)
        where TControl : Control

    {
        var collection = Interaction.GetBehaviors(control);
        collection.Add(behavior as AvaloniaObject);
        return control;
    }
}