using Avalonia.Controls;
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
                    new ColorPickerButton().Col(1).Color(() => effect?.Color ?? default, v => UpdateEffect(() => { if (effect != null) effect.Color = v; }))
                ),

            new TextBlock().Text("Offset X"),
            new Slider()
                .Minimum(-20)
                .Maximum(20)
                .Value(() => effect?.DeltaX ?? 0, v => UpdateEffect(() => { if (effect != null) effect.DeltaX = (float)v; })),

            new TextBlock().Text("Offset Y"),
            new Slider()
                .Minimum(-20)
                .Maximum(20)
                .Value(() => effect?.DeltaY ?? 0, v => UpdateEffect(() => { if (effect != null) effect.DeltaY = (float)v; })),

            new TextBlock().Text("Blur"),
            new Slider()
                .Minimum(0)
                .Maximum(200)
                .Value(() => effect?.Blur ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Blur = (float)v; })),

            new TextBlock().Text("Opacity"),
            new Slider()
                .Minimum(0)
                .Maximum(255)
                .Value(() => effect?.Opacity ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Opacity = (float)v; }))
        );
}