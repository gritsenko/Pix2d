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
///
/// Labels are drawn at a fixed *on-screen* size, so in world space they grow as the view zooms out while the
/// artboards do not: past some zoom the plaques start covering each other and the neighbouring artboards.
/// <see cref="EnumerateLabels"/> therefore runs a declutter pass every frame (see the constants below) —
/// pinned labels (active / selected / hovered artboard) always win, the rest drop out as soon as they would
/// overlap something, and below <see cref="MinArtboardPx"/> nothing is drawn at all. Hit-testing goes through
/// the same pass, so a hidden label is never a hidden click target either.
/// </summary>
public class ArtboardLabelsLayer : SKNode, IViewPortBindable
{
    private const float LabelFontPx = 13f;
    private const float LabelGapPx = 6f;   // gap between label bottom and artboard top
    private const float LabelPadXPx = 6f;
    private const float LabelPadYPx = 3f;

    /// <summary>On-screen artboard size (either dimension) below which no label is drawn at all — the plaque
    /// is ~25 px tall, so past this the name is bigger than the thing it names and every label is noise.</summary>
    private const float MinArtboardPx = 24f;

    /// <summary>How much of its own area a label may have covered by an already-shown label before it drops
    /// out. A small tolerance keeps two plaques that merely graze each other (common at 1:1 with long names)
    /// on screen; real crowding blows straight past it.</summary>
    private const float MaxLabelOverlapShare = 0.15f;

    /// <summary>How much of a label may land on top of *another* artboard's body before it drops out —
    /// measured against the label, not the body, because a plaque lying across the bottom edge of the row
    /// above reads as a mess whether that row is 16 px tall or 512. In effect a label is shown only while it
    /// (nearly) fits in the empty space above its own artboard: zoom in until the gutter can hold the plaque
    /// and the names come back.</summary>
    private const float MaxBodyIntrusionShare = 0.1f;

    private static readonly SKColor ActiveBg = new(0x29, 0xB0, 0xF3);
    private static readonly SKColor InactiveBg = new(0x33, 0x33, 0x33, 0xCC);
    private static readonly SKColor ActiveText = SKColors.White;
    private static readonly SKColor InactiveText = new(0xE0, 0xE0, 0xE0);

    private readonly Func<IEnumerable<Pix2dSprite>> _spritesProvider;
    private readonly Action<Pix2dSprite> _activateRequested;
    private readonly Action<Pix2dSprite> _editRequested;
    private readonly Func<Pix2dSprite, bool>? _isSelected;
    private readonly Action? _refreshRequested;
    private ViewPort? _vp;
    private Pix2dSprite? _hoveredSprite;

    public ArtboardLabelsLayer(Func<IEnumerable<Pix2dSprite>> spritesProvider,
        Action<Pix2dSprite> activateRequested, Action<Pix2dSprite> editRequested,
        Func<Pix2dSprite, bool>? isSelected = null, Action? refreshRequested = null)
    {
        _spritesProvider = spritesProvider;
        _activateRequested = activateRequested;
        _editRequested = editRequested;
        _isSelected = isSelected;
        _refreshRequested = refreshRequested;
        IsInteractive = true;
        Name = "Artboard labels";

        // Hovering an artboard body reveals its name even when the declutter pass dropped it — which is also
        // how you reach a hidden label's click target. The layer only gets pointer events over a *visible*
        // label (see ContainsPoint), so hover has to come from the global pointer instead.
        SKInput.Current.PointerChanged += OnGlobalPointerChanged;
    }

    private void OnGlobalPointerChanged(object? sender, SKInputPointer pointer)
    {
        Pix2dSprite? hovered = null;
        var sprites = _spritesProvider();
        if (sprites != null)
        {
            foreach (var sprite in sprites)
                if (sprite.GetBoundingBox().Contains(pointer.WorldPosition))
                    hovered = sprite; // last match wins — scene order is bottom-to-top
        }

        if (ReferenceEquals(hovered, _hoveredSprite))
            return;

        _hoveredSprite = hovered;
        _refreshRequested?.Invoke();
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

    /// <summary>An artboard whose label is never decluttered away: the one being edited, one selected in the
    /// General context, or the one under the pointer.</summary>
    private bool IsPinned(Pix2dSprite sprite)
        => sprite.EditMode
           || ReferenceEquals(sprite, _hoveredSprite)
           || (_isSelected?.Invoke(sprite) ?? false);

    /// <summary>
    /// The labels that are actually on screen this frame, in draw order. Pinned artboards are laid out first
    /// so that when two labels collide the more relevant one is the survivor; every other label is dropped as
    /// soon as it would bury an already-placed label or a neighbouring artboard. All the maths is in world
    /// units — the shares are ratios, so they hold at any zoom, and only the absolute
    /// <see cref="MinArtboardPx"/> cutoff needs converting.
    /// </summary>
    private IEnumerable<(Pix2dSprite Sprite, SKRect Rect)> EnumerateLabels(ViewPort vp)
    {
        var sprites = _spritesProvider()?.ToList();
        if (sprites == null || sprites.Count == 0)
            yield break;

        var minSize = vp.PixelsToWorld(MinArtboardPx);
        var bodies = sprites.Select(s => s.GetBoundingBox()).ToList();

        var order = sprites
            .Select((Sprite, Index) => (Sprite, Index))
            .OrderBy(t => IsPinned(t.Sprite) ? 0 : 1)
            .ThenBy(t => t.Index);

        var shown = new List<SKRect>(sprites.Count);

        foreach (var (sprite, index) in order)
        {
            var body = bodies[index];
            if (body.Width < minSize || body.Height < minSize)
                continue;

            var rect = GetLabelRect(vp, sprite);

            if (!IsPinned(sprite) && (IsBuriedByShownLabel(rect, shown) || IntrudesOnAnotherArtboard(rect, bodies, index)))
                continue;

            shown.Add(rect);
            yield return (sprite, rect);
        }
    }

    private static bool IsBuriedByShownLabel(SKRect rect, List<SKRect> shown)
    {
        var limit = rect.Width * rect.Height * MaxLabelOverlapShare;
        foreach (var other in shown)
        {
            if (IntersectionArea(rect, other) > limit)
                return true;
        }

        return false;
    }

    private static bool IntrudesOnAnotherArtboard(SKRect rect, List<SKRect> bodies, int ownIndex)
    {
        var limit = rect.Width * rect.Height * MaxBodyIntrusionShare;
        for (var i = 0; i < bodies.Count; i++)
        {
            if (i == ownIndex)
                continue;

            if (IntersectionArea(rect, bodies[i]) > limit)
                return true;
        }

        return false;
    }

    private static float IntersectionArea(SKRect a, SKRect b)
    {
        var w = MathF.Min(a.Right, b.Right) - MathF.Max(a.Left, b.Left);
        var h = MathF.Min(a.Bottom, b.Bottom) - MathF.Max(a.Top, b.Top);
        return w <= 0 || h <= 0 ? 0 : w * h;
    }
}
