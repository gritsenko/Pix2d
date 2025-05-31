using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class PixelGlowEffectSettingsView(PixelGlowEffect e, Action onEffectUpdated)
    : EffectSettingsViewBase<PixelGlowEffect>(e, onEffectUpdated)
{
    protected override object Build(PixelGlowEffect? effect) =>
        new StackPanel().Children(
            new TextBlock().Text("Radius"),
            new Slider()
                .Maximum(10)
                .Minimum(-10)
                .SmallChange(1)
                .LargeChange(3)
                .Value(() => effect.Radius, v => UpdateEffect(() => effect.Radius = (float)v)),

            new TextBlock().Text("Blur amount"),
            new Slider()
                .Maximum(20)
                .Minimum(0)
                .SmallChange(0.1)
                .LargeChange(3)
                .Value(() => effect.Blur, v => UpdateEffect(() => effect.Blur = (float)v)),

            new TextBlock().Text("Opacity"),
            new Slider()
                .Maximum(255)
                .Minimum(0)
                .SmallChange(1)
                .LargeChange(5)
                .Value(() => effect.Opacity, v => UpdateEffect(() => effect.Opacity = (int)v))
        );
}