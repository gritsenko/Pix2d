using SkiaSharp;

namespace SkiaNodes.Render;

public readonly struct RenderContext(
    SKCanvas canvas,
    ViewPort viewPort,
    float opacity = 1f
    //IReadOnlyList<ISKNodeEffect>? inheritedEffects = null
)
{
    public SKCanvas Canvas { get; } = canvas;
    public ViewPort ViewPort { get; } = viewPort;
    public float Opacity { get; } = opacity;

    //public IReadOnlyList<ISKNodeEffect> InheritedEffects { get; } =
    //    inheritedEffects ?? Array.Empty<ISKNodeEffect>();
}