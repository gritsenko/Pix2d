using SkiaNodes.Render;
using SkiaSharp;

namespace SkiaNodes;

public interface ISKNodeEffect
{
    string Name { get; }
    EffectType EffectType { get; }
    void Render(RenderContext rc, SKSurface source);
    void Invalidate();
}

public enum EffectType
{
    ReplaceEffect = -1,
    BackEffect = 0,
    OverlayEffect = 1,
}