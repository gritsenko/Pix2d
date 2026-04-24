using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class PixelBlurEffectSettingsView(PixelBlurEffect e, Action onEffectUpdated)
    : EffectSettingsViewBase<PixelBlurEffect>(e, onEffectUpdated)
{
    protected override object BuildEffectSettings(PixelBlurEffect effect) =>
        new StackPanel().Children(
            new TextBlock().Text("Blur amount"),
            CreateSlider(0, 20, effect.Blur, value => effect.Blur = (float)value, 0.1, 3)
        );
}