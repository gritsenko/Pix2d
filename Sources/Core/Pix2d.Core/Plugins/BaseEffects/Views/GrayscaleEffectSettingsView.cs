using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Abstract.Services;
using Pix2d.Effects;
using Pix2d.UI;

namespace Pix2d.Plugins.BaseEffects.Views;

public class GrayscaleEffectSettingsView : ViewBase, IEffectsService.IEffectSettingsView
{
    public GrayscaleEffectSettingsView(GrayscaleEffect _)
    {
    }

    protected override object Build() =>
        new StackPanel().Children(
            new TextBlock().Text("No settings")
        );
}