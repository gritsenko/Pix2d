using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class PixelGlowEffectSettingsView(PixelGlowEffect e, Action onEffectUpdated)
    : EffectSettingsViewBase<PixelGlowEffect>(e, onEffectUpdated)
{
    protected override object BuildEffectSettings(PixelGlowEffect effect) =>
        new StackPanel().Children(
            new TextBlock().Text("Radius"),
            CreateSlider(-10, 10, effect.Radius, value => effect.Radius = (float)value, 1, 3),

            new TextBlock().Text("Blur amount"),
            CreateSlider(0, 20, effect.Blur, value => effect.Blur = (float)value, 0.1, 3),

            new TextBlock().Text("Opacity"),
            CreateSlider(0, 255, effect.Opacity, value => effect.Opacity = (int)value, 1, 5)
        );
}