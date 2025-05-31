using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.BaseEffects.Views;

public class ShadowEffectSettingsView(PixelShadowEffect effect, Action onEffectUpdated) : EffectSettingsViewBase<PixelShadowEffect>(effect, onEffectUpdated)
{
    protected override object Build(PixelShadowEffect? effect) =>
        new StackPanel().Children(
            new Grid().Cols("*, Auto")
                .Children(
                    new TextBlock().Col(0).Text("Color"),
                    new ColorPickerButton().Col(1).Color(effect.Color, BindingMode.TwoWay, bindingSource: effect)
                ),

            new TextBlock().Text("Offset X"),
            new Slider()
                .Minimum(-20)
                .Maximum(20)
                .Value(() => effect.DeltaX, v => UpdateEffect(() => effect.DeltaX = (float)v)),

            new TextBlock().Text("Offset Y"),
            new Slider()
                .Minimum(-20)
                .Maximum(20)
                .Value(() => effect.DeltaY, v => UpdateEffect(() => effect.DeltaY = (float)v)),

            new TextBlock().Text("Blur"),
            new Slider()
                .Minimum(0)
                .Maximum(200)
                .Value(() => effect.Blur, v => UpdateEffect(() => effect.Blur = (float)v)),

            new TextBlock().Text("Opacity"),
            new Slider()
                .Minimum(0)
                .Maximum(255)
                .Value(() => effect.Opacity, v => UpdateEffect(() => effect.Opacity = (float)v))
        );
}