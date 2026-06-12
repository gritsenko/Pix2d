#nullable enable
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

/// <summary>
/// A single always-on overlay node (lives in the scene's <see cref="AdornerLayer"/>) that draws every
/// artboard's name above its top-left corner at a fixed on-screen size, and turns a double-click on a
/// label into an "edit sprite as object" request.
///
/// It reads the live scene each frame via <see cref="_spritesProvider"/>, so adding / removing / renaming /
/// moving artboards needs no bookkeeping. Because it sits in the adorner layer it is only rendered while
/// <c>vp.Settings.RenderAdorners</c> is true → labels never leak into thumbnails or exports.
/// </summary>
public class ArtboardLabelsLayer : SKNode, IViewPortBindable
{
    private const float LabelFontPx = 13f;
    private const float LabelGapPx = 6f;   // gap between label bottom and artboard top
    private const float LabelPadXPx = 6f;
    private const float LabelPadYPx = 3f;

    private static readonly SKColor ActiveBg = new(0x29, 0xB0, 0xF3);
    private static readonly SKColor InactiveBg = new(0x33, 0x33, 0x33, 0xCC);
    private static readonly SKColor ActiveText = SKColors.White;
    private static readonly SKColor InactiveText = new(0xE0, 0xE0, 0xE0);

    private readonly Func<IEnumerable<Pix2dSprite>> _spritesProvider;
    private readonly Action<Pix2dSprite> _activateRequested;
    private readonly Action<Pix2dSprite> _editRequested;
    private ViewPort? _vp;

    public ArtboardLabelsLayer(Func<IEnumerable<Pix2dSprite>> spritesProvider,
        Action<Pix2dSprite> activateRequested, Action<Pix2dSprite> editRequested)
    {
        _spritesProvider = spritesProvider;
        _activateRequested = activateRequested;
        _editRequested = editRequested;
        IsInteractive = true;
        Name = "Artboard labels";
    }

    public void OnViewChanged(ViewPort vp) => _vp = vp;

    // The node itself has no bounds; it is a hit target only over the label rectangles.
    public override bool ContainsPoint(SKPoint worldPos) => TryGetSpriteAt(worldPos, out _);

    public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
        base.OnPointerPressed(eventArgs, clickCount);

        if (!TryGetSpriteAt(eventArgs.Pointer.WorldPosition, out var sprite) || sprite == null)
            return;

        // Consume any press inside a label so it never starts a stray brush stroke in the empty space
        // above the artboard. A single click makes the artboard active; a double-click enters object-edit mode.
        eventArgs.Handled = true;
        if (clickCount == 2)
            _editRequested(sprite);
        else
            _activateRequested(sprite);
    }

    private bool TryGetSpriteAt(SKPoint worldPos, out Pix2dSprite? sprite)
    {
        var vp = _vp;
        if (vp != null)
        {
            foreach (var (s, rect) in EnumerateLabels(vp))
            {
                if (rect.Contains(worldPos))
                {
                    sprite = s;
                    return true;
                }
            }
        }

        sprite = null;
        return false;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        _vp = vp;

        var fontSize = vp.PixelsToWorld(LabelFontPx);
        var radius = vp.PixelsToWorld(3);
        var padX = vp.PixelsToWorld(LabelPadXPx);

        using var font = new SKFont { Size = fontSize };
        using var bgPaint = new SKPaint { IsAntialias = true };
        using var textPaint = new SKPaint { IsAntialias = true };

        canvas.Save();
        canvas.SetMatrix(vp.ResultTransformMatrix);

        foreach (var (sprite, rect) in EnumerateLabels(vp))
        {
            var accent = sprite.EditMode;
            bgPaint.Color = accent ? ActiveBg : InactiveBg;
            textPaint.Color = accent ? ActiveText : InactiveText;

            canvas.DrawRoundRect(rect, radius, radius, bgPaint);

            var baseline = rect.Top - font.Metrics.Ascent + vp.PixelsToWorld(LabelPadYPx);
            canvas.DrawText(GetName(sprite), rect.Left + padX, baseline, SKTextAlign.Left, font, textPaint);
        }

        canvas.Restore();
    }

    private static string GetName(Pix2dSprite sprite)
        => string.IsNullOrWhiteSpace(sprite.Name) ? "Artboard" : sprite.Name;

    /// <summary>
    /// World-space rectangle of a single artboard's name label, at the same fixed on-screen size used for
    /// drawing. Exposed so the object-edit overlay can place its move handle exactly over the label
    /// (drag-by-label) without duplicating the layout maths.
    /// </summary>
    public static SKRect GetLabelRect(ViewPort vp, Pix2dSprite sprite)
    {
        var fontSize = vp.PixelsToWorld(LabelFontPx);
        var gap = vp.PixelsToWorld(LabelGapPx);
        var padX = vp.PixelsToWorld(LabelPadXPx);
        var padY = vp.PixelsToWorld(LabelPadYPx);

        using var font = new SKFont { Size = fontSize };
        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent;

        var bb = sprite.GetBoundingBox();
        var textWidth = font.MeasureText(GetName(sprite));

        var height = lineHeight + padY * 2;
        var bottom = bb.Top - gap;
        var top = bottom - height;
        return new SKRect(bb.Left, top, bb.Left + textWidth + padX * 2, bottom);
    }

    private IEnumerable<(Pix2dSprite Sprite, SKRect Rect)> EnumerateLabels(ViewPort vp)
    {
        var sprites = _spritesProvider();
        if (sprites == null)
            yield break;

        foreach (var sprite in sprites)
            yield return (sprite, GetLabelRect(vp, sprite));
    }
}
