using System.Runtime.CompilerServices;
using SkiaSharp;

namespace SkiaNodes.Render;

public class RenderCacheManager
{
    public static readonly RenderCacheManager Instance = new();

    private readonly ConditionalWeakTable<SKNode, SKSurface> _surfaces = new();

    public SKSurface GetSurface(SKNode node)
    {
        var size = node.Size;
        if (!_surfaces.TryGetValue(node, out var surface))
        {
            surface?.Dispose();
            var info = new SKImageInfo((int)size.Width, (int)size.Height);
            surface = SKSurface.Create(info);
            _surfaces.AddOrUpdate(node, surface);
        }

        return surface;
    }

    public void Invalidate(SKNode node) => _surfaces.Remove(node);
}