using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.BaseEffects.Views;

public class ColorOverlayEffectSettingsView(ColorOverlayEffect e, Action onEffectUpdated) : EffectSettingsViewBase<ColorOverlayEffect>(e, onEffectUpdated)
{
    protected override object BuildEffectSettings(ColorOverlayEffect effect) =>
        new StackPanel().Children(
            new Grid().Cols("*, Auto")
                .Children(
                    new TextBlock().Col(0).Text("Color"),
                    CreateColorPicker(effect.Color, value => effect.Color = value).Col(1)
                ),
            CreateSliderEx("Opacity", 0, 255, effect.Opacity, value => effect.Opacity = (float)value, "%")
        );
}