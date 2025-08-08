using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.BaseEffects.Views;

public class ColorOverlayEffectSettingsView(ColorOverlayEffect e, Action onEffectUpdated) : EffectSettingsViewBase<ColorOverlayEffect>(e, onEffectUpdated)
{
    protected override object Build(ColorOverlayEffect? effect) =>
        new StackPanel().Children(
            new Grid().Cols("*, Auto")
                .Children(
                    new TextBlock().Col(0).Text("Color"),
                    new ColorPickerButton().Col(1)
                        .Color(() => effect?.Color ?? default, v => UpdateEffect(() => { if (effect != null) effect.Color = v; }))
                ),
            new SliderEx()
                .Minimum(0)
                .Maximum(255)
                .Label("Opacity")
                .Units("%")
                .Value(() => effect?.Opacity ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Opacity = (float)v; }))
        );
}