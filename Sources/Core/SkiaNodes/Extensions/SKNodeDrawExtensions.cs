using SkiaNodes.Render;
using SkiaSharp;

namespace SkiaNodes.Extensions;

public static class SKNodeDrawExtensions
{
    private static readonly SKColor BoundingBoxColor = new((byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256));

    public static void DrawDebugStuff(this SKNode node, RenderContext rc)
    {
        var vp = rc.ViewPort;
        var canvas = rc.Canvas;

        DrawHitZone(node, canvas, vp, 2, SKColors.Red);

        DrawBoundingBox(node, canvas, vp, 2, BoundingBoxColor);

        using var paint = new SKPaint();
        paint.Color = BoundingBoxColor;
        paint.TextSize = 14;
        canvas.DrawText($"{node.Name}[{node.GetType().Name}]", 10 * node.GetNestingLevel(), 20 + 20 * node.Index, paint);
    }

    public static void DrawBoundingBox(SKNode node, SKCanvas canvas, ViewPort vp, float thickness, SKColor color) =>
        DrawRect(node.GetBoundingBox(), canvas, vp, thickness, color);

    public static void DrawHitZone(SKNode node, SKCanvas canvas, ViewPort vp, float thickness, SKColor color) =>
        DrawRect(node.GetHitZone(), canvas, vp, thickness, color);

    public static void DrawRect(SKRect bbox, SKCanvas canvas, ViewPort vp, float thickness, SKColor color)
    {
        canvas.Save();

        var transform = vp.ResultTransformMatrix;
        canvas.SetMatrix(transform);

        using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(thickness), color);
        canvas.DrawRect(bbox.Left, bbox.Top, bbox.Width, bbox.Height, paint);

        canvas.Restore();
    }


}