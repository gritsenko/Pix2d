using Avalonia.Controls;
using Avalonia.Markup.Declarative;
using Pix2d.Effects;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.BaseEffects.Views;

public class ShadowEffectSettingsView(PixelShadowEffect effect, Action onEffectUpdated) : EffectSettingsViewBase<PixelShadowEffect>(effect, onEffectUpdated)
{
    protected override object BuildEffectSettings(PixelShadowEffect effect) =>
        new StackPanel().Children(
            new Grid().Cols("*, Auto")
                .Children(
                    new TextBlock().Col(0).Text("Color"),
                    CreateColorPicker(effect.Color, value => effect.Color = value).Col(1)
                ),

            new TextBlock().Text("Offset X"),
            CreateSlider(-20, 20, effect.DeltaX, value => effect.DeltaX = (float)value),

            new TextBlock().Text("Offset Y"),
            CreateSlider(-20, 20, effect.DeltaY, value => effect.DeltaY = (float)value),

            new TextBlock().Text("Blur"),
            CreateSlider(0, 200, effect.Blur, value => effect.Blur = (float)value),

            new TextBlock().Text("Opacity"),
            CreateSlider(0, 255, effect.Opacity, value => effect.Opacity = (float)value)
        );
}