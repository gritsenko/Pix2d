using Pix2d.Effects;

namespace Pix2d.Plugins.BaseEffects.Views;

public class OutlineEffectSettingsView(OutlineEffect e, Action onEffectUpdated) : EffectSettingsViewBase<OutlineEffect>(e, onEffectUpdated)
{
    protected override object BuildEffectSettings(OutlineEffect effect) =>
        new StackPanel().Children(
            new Grid().Cols("*, Auto")
                .Children(
                    new TextBlock().Col(0).Text("Color"),
                    CreateColorPicker(effect.Color, value => effect.Color = value).Col(1)
                ),

            CreateSliderEx(L("Thickness"), 1, 20, effect.Radius, value => effect.Radius = (float)value)
        );
}