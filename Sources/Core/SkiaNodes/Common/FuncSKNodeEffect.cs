using SkiaNodes.Render;
using SkiaSharp;

namespace SkiaNodes.Common;

public class FuncSKNodeEffect(string name, EffectType effectType, Action<RenderContext, SKSurface> renderAction) : ISKNodeEffect
{
    public string Name => name;
    public EffectType EffectType => effectType;
    public void Render(RenderContext rc, SKSurface source) => renderAction(rc, source);

    public void Invalidate()
    {
    }
}