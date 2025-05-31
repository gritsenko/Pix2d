using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Abstract.Services;
using Pix2d.Effects;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.BaseEffects.Views;

public class GrayscaleEffectSettingsView(GrayscaleEffect e)
    : LocalizedComponentBase<GrayscaleEffect>(e), IEffectsService.IEffectSettingsView
{
    protected override object Build(GrayscaleEffect? effect) =>

        new StackPanel().Children(
            new TextBlock().Text("No settings")
        );
}