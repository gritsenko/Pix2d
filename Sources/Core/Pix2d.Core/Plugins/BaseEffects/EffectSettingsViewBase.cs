using Pix2d.UI.Shared;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.BaseEffects;

public abstract class EffectSettingsViewBase<TEffect> : ViewBase, IEffectsService.IEffectSettingsView
    where TEffect : ISKNodeEffect
{
    private readonly TEffect _effect;
    private readonly Action _onEffectUpdated;

    protected EffectSettingsViewBase(TEffect effect, Action onEffectUpdated)
    {
        _effect = effect;
        _onEffectUpdated = onEffectUpdated;
    }

    protected override object Build() => BuildEffectSettings(_effect);

    protected abstract object BuildEffectSettings(TEffect effect);

    protected void UpdateEffect(Action updatePropertyFunc)
    {
        updatePropertyFunc.Invoke();
        _effect.Invalidate();
        _onEffectUpdated();
    }

    protected ColorPickerButton CreateColorPicker(SKColor value, Action<SKColor> onChanged)
    {
        return new ColorPickerButton()
            .Color(value)
            .OnColorChanged(c => UpdateEffect(() => onChanged(c.NewColor)));
    }

    protected SliderEx CreateSliderEx(string label, double minimum, double maximum, double value, Action<double> onChanged, string units = "")
    {
        return new SliderEx()
            .Label(label)
            .Minimum(minimum)
            .Maximum(maximum)
            .Units(units)
            .Value(value)
            .OnValueChanged(e => UpdateEffect(() => onChanged(e)));
    }

    protected Slider CreateSlider(double minimum, double maximum, double value, Action<double> onChanged, double smallChange = 1, double largeChange = 10)
    {
        return new Slider()
            .Minimum(minimum)
            .Maximum(maximum)
            .SmallChange(smallChange)
            .LargeChange(largeChange)
            .Value(value)
            .OnValueChanged(e => UpdateEffect(() => onChanged(e.NewValue)));
    }
}