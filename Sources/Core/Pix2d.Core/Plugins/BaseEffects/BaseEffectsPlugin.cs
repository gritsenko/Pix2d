using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Effects;
using Pix2d.Plugins.BaseEffects.Views;

namespace Pix2d.Plugins.BaseEffects;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(BaseEffectsPlugin))]
public class BaseEffectsPlugin(IEffectsService effectsService, IViewPortRefreshService refreshService) : IPix2dPlugin
{
    public void Initialize()
    {
        effectsService.RegisterEffect<ColorOverlayEffect>("Color overlay", e => new ColorOverlayEffectSettingsView(e, onEffectUpdated: Refresh));
        effectsService.RegisterEffect<PixelShadowEffect>("Shadow", e => new ShadowEffectSettingsView(e, onEffectUpdated: Refresh));
        effectsService.RegisterEffect<PixelBlurEffect>("Blur", e => new PixelBlurEffectSettingsView(e, onEffectUpdated: Refresh));
        effectsService.RegisterEffect<PixelGlowEffect>("Glow", e => new PixelGlowEffectSettingsView(e, onEffectUpdated: Refresh));
        effectsService.RegisterEffect<OutlineEffect>("Outline", e => new OutlineEffectSettingsView(e, onEffectUpdated: Refresh));
        effectsService.RegisterEffect<GrayscaleEffect>("Grayscale", e => new GrayscaleEffectSettingsView(e));
        effectsService.RegisterEffect<ImageAdjustEffect>("Image adjust", e => new ImageAdjustEffectSettingsView(e, onEffectUpdated: Refresh));
    }

    private void Refresh()
    {
        refreshService.Refresh();
    }
}