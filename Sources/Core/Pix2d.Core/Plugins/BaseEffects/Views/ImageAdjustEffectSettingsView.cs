using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class ImageAdjustEffectSettingsView(ImageAdjustEffect e, Action onEffectUpdated)
    : EffectSettingsViewBase<ImageAdjustEffect>(e, onEffectUpdated)
{
    protected override object Build(ImageAdjustEffect? effect) =>

        new StackPanel().Children(
            new TextBlock().Text("Hue"),
            new Slider()
                .Minimum(-180)
                .Maximum(180)
                .Value(() => effect?.Hue ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Hue = (float)v; })),
            new TextBlock().Text("Brightness"),
            new Slider()
                .Minimum(-100)
                .Maximum(100)
                .Value(() => effect?.Lightness ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Lightness = (float)v; })),
            new TextBlock().Text("Saturation"),
            new Slider()
                .Minimum(-100)
                .Maximum(100)
                .Value(() => effect?.Saturation ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Saturation = (float)v; }))
        );
}