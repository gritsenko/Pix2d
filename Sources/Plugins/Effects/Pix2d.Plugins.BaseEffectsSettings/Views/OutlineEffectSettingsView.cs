using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.BaseEffects.Views;

public class OutlineEffectSettingsView(OutlineEffect e, Action onEffectUpdated) : EffectSettingsViewBase<OutlineEffect>(e, onEffectUpdated)
{
    protected override object Build(OutlineEffect? effect) =>
        new StackPanel().Children(
            new Grid().Cols("*, Auto")
                .Children(
                    new TextBlock().Col(0).Text("Color"),
                    new ColorPickerButton().Col(1)
                        .Color(() => effect?.Color ?? default, v => UpdateEffect(() => { if (effect != null) effect.Color = v; }))
                ),

            new SliderEx()
                .Minimum(1)
                .Maximum(20)
                .Label(L("Thickness"))
                .Value(() => effect?.Radius ?? 0, v => UpdateEffect(() => { if (effect != null) effect.Radius = (float)v; }))
        );
}