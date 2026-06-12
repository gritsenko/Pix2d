#nullable enable
using System.Globalization;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

/// <summary>
/// Small floating HUD badge that hugs the bottom edge of an editing frame and shows the live position,
/// size and (when non-zero) rotation of the edited region. Shared by every framing affordance — the crop
/// tool, the pixel-selection transform frame, and the "edit sprite as object" frame — so the readout looks
/// and behaves identically across them.
///
/// Like the resize thumbs it draws under <see cref="ViewPort.ResultTransformMatrix"/> in world coordinates
/// but sizes everything via <see cref="ViewPort.PixelsToWorld"/>, so the badge stays a constant on-screen
/// size regardless of zoom. It pulls its values from <see cref="InfoProvider"/> on every frame, so it tracks
/// a drag live without any per-delta wiring; returning null hides it for that frame.
/// </summary>
public class FrameInfoBadgeNode : SKNode
{
    public readonly record struct FrameInfo(SKRect WorldBounds, SKPoint Position, SKSize Size, float Rotation);

    private const float FontPx = 12f;
    private const float PadXPx = 8f;
    private const float PadYPx = 4f;
    private const float GapPx = 14f; // gap between the frame edge and the badge

    private static readonly SKColor BgColor = new(0xE5, 0x1A, 0x6B); // pink, matches the cursor-coordinate pill
    private static readonly SKColor TextColor = SKColors.White;

    /// <summary>Supplies the values to display each frame. Return null to hide the badge.</summary>
    public Func<FrameInfo?>? InfoProvider { get; set; }

    // Purely decorative — never participate in hit-testing so it can't steal pointer events from the thumbs.
    public override bool ContainsPoint(SKPoint worldPos) => false;

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (InfoProvider?.Invoke() is not { } info)
            return;

        var text = BuildText(info);

        var fontSize = vp.PixelsToWorld(FontPx);
        var padX = vp.PixelsToWorld(PadXPx);
        var padY = vp.PixelsToWorld(PadYPx);
        var gap = vp.PixelsToWorld(GapPx);
        var radius = vp.PixelsToWorld(4);

        using var font = new SKFont { Size = fontSize };
        using var bgPaint = new SKPaint { IsAntialias = true, Color = BgColor };
        using var textPaint = new SKPaint { IsAntialias = true, Color = TextColor };

        var textWidth = font.MeasureText(text);
        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        var w = textWidth + padX * 2;
        var h = lineHeight + padY * 2;

        var cx = info.WorldBounds.MidX;
        var top = info.WorldBounds.Bottom + gap;

        // Flip above the frame if the badge would fall outside the visible area below it.
        var visible = vp.GetVisibleArea();
        if (top + h > visible.Bottom)
            top = info.WorldBounds.Top - gap - h;

        var rect = new SKRect(cx - w / 2f, top, cx + w / 2f, top + h);

        canvas.Save();
        canvas.SetMatrix(vp.ResultTransformMatrix);
        canvas.DrawRoundRect(rect, radius, radius, bgPaint);
        var baseline = rect.Top + padY - font.Metrics.Ascent;
        canvas.DrawText(text, rect.MidX, baseline, SKTextAlign.Center, font, textPaint);
        canvas.Restore();
    }

    private static string BuildText(FrameInfo info)
    {
        var x = (int)MathF.Round(info.Position.X);
        var y = (int)MathF.Round(info.Position.Y);
        var w = (int)MathF.Round(info.Size.Width);
        var h = (int)MathF.Round(info.Size.Height);

        var text = $"X: {x}  Y: {y}    {w} × {h}";

        if (MathF.Abs(info.Rotation % 360f) > 0.05f)
        {
            var angle = info.Rotation % 360f;
            text += $"    {angle.ToString("0.#", CultureInfo.InvariantCulture)}°";
        }

        return text;
    }
}
