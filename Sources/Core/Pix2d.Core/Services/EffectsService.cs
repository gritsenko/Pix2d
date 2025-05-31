using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.Operations.Effects;
using Pix2d.Plugins.Sprite.Operations.Effects;
using SkiaNodes;

namespace Pix2d.Services;

public class EffectsService(IOperationService operationService) : IEffectsService
{
    private readonly List<IEffectsService.IEffectItem> _availableEffects = [];

    public IEnumerable<IEffectsService.IEffectItem> GetAvailableEffects()
    {
        return _availableEffects;
    }

    public void RegisterEffect<TEffect>(string title, Func<TEffect, IEffectsService.IEffectSettingsView> settingsViewFuc)
    {
        _availableEffects.Add(new RegisteredEffectItem<TEffect, IEffectsService.IEffectSettingsView>(title, settingsViewFuc));
    }

    public void RemoveEffect(SKNode node, ISKNodeEffect effect)
    {
        operationService.InvokeAndPushOperations(new RemoveEffectOperation(node, effect));
    }

    public void BakeEffect(SKNode node, ISKNodeEffect effect)
    {
        if (node is not Pix2dSprite.Layer)
            throw new NotSupportedException("Only sprites are supported");

        operationService.InvokeAndPushOperations(new BakeEffectOperation(node, effect));
    }

    public void AddEffect(SKNode node, IEffectsService.IEffectItem effectItem)
    {
        var effect = Activator.CreateInstance(effectItem.EffectType) as ISKNodeEffect;
        operationService.InvokeAndPushOperations(new AddEffectOperation([node], effect));
    }

    public IEffectsService.IEffectSettingsView GetSettingsView(ISKNodeEffect effect)
    {
        var effectItem = _availableEffects.FirstOrDefault(x => x.EffectType == effect.GetType());
        return effectItem.GetSettingsView(effect);
    }

    public class RegisteredEffectItem<TEffect, TEffectSettingsView>(
        string title,
        Func<TEffect, TEffectSettingsView> settingsViewFuc)
        : IEffectsService.IEffectItem
    {
        public Func<TEffect, TEffectSettingsView> SettingsViewFuc { get; } = settingsViewFuc;
        public string Title { get; } = title;
        public Type EffectType { get; } = typeof(TEffect);

        public IEffectsService.IEffectSettingsView GetSettingsView(ISKNodeEffect effect) =>
            (IEffectsService.IEffectSettingsView)SettingsViewFuc.Invoke((TEffect)effect);
    }
}