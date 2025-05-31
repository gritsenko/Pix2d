using System.Runtime.CompilerServices;
using SkiaNodes.Common;
using SkiaSharp;

namespace SkiaNodes.Render;

public class SKNodeRenderer
{
    private static readonly ISKNodeEffect BypassEffect = new BypassEffect();

    public static void Render(SKNode node, RenderContext rc)
    {
        rc.Canvas.SetMatrix(rc.ViewPort.ResultTransformMatrix);
        _render(node, rc);
    }

    private static void _render(SKNode node, in RenderContext rc)
    {
        rc.Canvas.Save();

        using (var scope = new RenderScope(node, rc))
        {
            RenderNodeWithAppliedEffects(node, rc);
            RenderChildren(node, scope.CreateChildContext());
            RenderAdorner(node, rc, scope.AdornerTransform);
        }

        rc.Canvas.Restore();
    }

    private static void RenderNodeWithAppliedEffects(SKNode node, in RenderContext rc)
    {
        if (node.ShowEffects && node.Effects.Count != 0)
            rc.Canvas.DrawSurface(
                RenderEffectsToSurface(node), 0, 0);
        else
            node.OnDraw(rc.Canvas, rc.ViewPort);
    }

    private static SKSurface RenderEffectsToSurface(SKNode node)
    {
        var surface = RenderCacheManager.Instance.GetSurface(node);
        var effectRc = new RenderContext(
            surface.Canvas,
            new ViewPort((int)node.Size.Width, (int)node.Size.Height));

        effectRc.Canvas.Clear(SKColors.Transparent);
        node.OnDraw(effectRc.Canvas, effectRc.ViewPort);

        foreach (var effect in GetOrderedEffects(node))
            effect.Render(effectRc, surface);

        return surface;
    }

    private static IEnumerable<ISKNodeEffect> GetOrderedEffects(SKNode node)
    {
        var orderedEffects = node.Effects.OrderBy(x => x.EffectType);
        var i = 0;
        foreach (var orderedEffect in orderedEffects)
        {
            if (i == 0 && orderedEffect.EffectType != EffectType.ReplaceEffect)
                yield return BypassEffect;

            yield return orderedEffect;
            i++;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RenderChildren(SKNode parent, in RenderContext rc)
    {
        foreach (var child in parent.Nodes.AsSpan())
            if (child.IsVisible)
                _render(child, rc);
    }

    protected static void RenderAdorner(SKNode node, in RenderContext rc, SKMatrix adornerTransform)
    {
        if (!node.HasAdornerLayer
            || !rc.ViewPort.Settings.RenderAdorners
            || !node.AdornerLayer.Nodes.Any())
            return;

        rc.Canvas.Save();
        rc.Canvas.SetMatrix(adornerTransform);
        _render(node.AdornerLayer, rc);
        rc.Canvas.Restore();
    }
}