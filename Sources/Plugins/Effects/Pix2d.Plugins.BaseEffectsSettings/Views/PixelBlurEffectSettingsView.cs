using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class PixelBlurEffectSettingsView(PixelBlurEffect e, Action onEffectUpdated)
    : EffectSettingsViewBase<PixelBlurEffect>(e, onEffectUpdated)
{
    protected override object Build(PixelBlurEffect? effect) =>
        new StackPanel().Children(
            new TextBlock().Text("Blur amount"),
            new Slider()
                .Maximum(20)
                .Minimum(0)
                .SmallChange(0.1)
                .LargeChange(3)
                .Value(() => effect.Blur, v => UpdateEffect(()=> effect.Blur = (float)v))
        );
}