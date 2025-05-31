namespace SkiaNodes.Common;

public class BypassEffect() : FuncSKNodeEffect("BypassEffect", EffectType.ReplaceEffect, (rc, source) => rc.Canvas.DrawSurface(source, 0, 0));