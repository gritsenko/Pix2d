using SkiaNodes;

namespace Pix2d.Abstract.Services;

public interface IEffectsService
{
    IEnumerable<IEffectItem> GetAvailableEffects();
    void RegisterEffect<TEffect>(string title, Func<TEffect, IEffectSettingsView> settingsViewFuc);
    void RemoveEffect(SKNode node, ISKNodeEffect effect);
    void BakeEffect(SKNode node, ISKNodeEffect effect);
    void AddEffect(SKNode node, IEffectItem effectItem);
    IEffectSettingsView GetSettingsView(ISKNodeEffect effect);

    public interface IEffectItem
    {
        string Title { get; }
        Type EffectType { get; }
        IEffectSettingsView GetSettingsView(ISKNodeEffect effect);
    }
    public interface IEffectSettingsView;
}