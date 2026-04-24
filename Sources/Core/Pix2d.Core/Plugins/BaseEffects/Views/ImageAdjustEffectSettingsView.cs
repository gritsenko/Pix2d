using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class ImageAdjustEffectSettingsView(ImageAdjustEffect e, Action onEffectUpdated)
    : EffectSettingsViewBase<ImageAdjustEffect>(e, onEffectUpdated)
{
    protected override object BuildEffectSettings(ImageAdjustEffect effect) =>
        new StackPanel().Children(
            new TextBlock().Text("Hue"),
            CreateSlider(-180, 180, effect.Hue, value => effect.Hue = (float)value),
            new TextBlock().Text("Brightness"),
            CreateSlider(-100, 100, effect.Lightness, value => effect.Lightness = (float)value),
            new TextBlock().Text("Saturation"),
            CreateSlider(-100, 100, effect.Saturation, value => effect.Saturation = (float)value)
        );
}