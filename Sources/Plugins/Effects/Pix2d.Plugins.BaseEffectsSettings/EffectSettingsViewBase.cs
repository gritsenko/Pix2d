using Pix2d.Abstract.Services;
using Pix2d.UI.Shared;
using SkiaNodes;

namespace Pix2d.Plugins.BaseEffects;

public abstract class EffectSettingsViewBase<TEffect>(TEffect effect, Action onEffectUpdated) : LocalizedComponentBase<TEffect>(effect),
    IEffectsService.IEffectSettingsView
    where TEffect : ISKNodeEffect
{
    private TEffect _effect = effect;

    protected void UpdateEffect(Action updatePropertyFunc)
    {
        updatePropertyFunc.Invoke();
        _effect.Invalidate();
        onEffectUpdated();
    }
}