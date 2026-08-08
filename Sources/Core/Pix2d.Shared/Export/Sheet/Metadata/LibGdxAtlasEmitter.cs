#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Emits a libGDX <c>TextureAtlas</c> descriptor (<c>.atlas</c>), the format
/// <c>TextureAtlas(Gdx.files.internal("hero.atlas"))</c> reads directly.
///
/// The load-bearing detail is <b>region name + index</b>, not the geometry: libGDX's
/// <c>atlas.findRegions("run")</c> returns every region sharing that name ordered by <c>index</c>, which is
/// what <c>new Animation&lt;&gt;(frameDuration, atlas.findRegions("run"))</c> consumes. So each animation tag
/// becomes a region *name* repeated once per frame, with <c>index</c> counting within that tag — an untagged
/// sprite emits its own name instead, and its frames are still an ordered, playable region list.
///
/// Two things the format cannot carry, and therefore neither can this emitter:
/// <list type="bullet">
/// <item><b>Per-frame durations.</b> libGDX takes one <c>frameDuration</c> in the <c>Animation</c>
/// constructor, so a sprite's per-frame overrides are dropped (the Aseprite JSON preset keeps them if that
/// matters). The sprite's frame rate is not expressible either — pass <c>1f / fps</c> in code.</item>
/// <item><b>Pivot / 9-slice.</b> libGDX reads a nine-patch from <c>.9.png</c> split pixels, not from the
/// atlas descriptor; the <c>split</c>/<c>pad</c> region properties exist but only TexturePacker's own
/// nine-patch pipeline writes them, so a 9-slice sprite exports as a plain region.</item>
/// </list>
///
/// Written in the pre-1.9.9 spaced form (<c>size: w,h</c>), which every libGDX version's
/// <c>TextureAtlasData</c> parser accepts — the newer unspaced form is not readable by older runtimes.
/// </summary>
public sealed class LibGdxAtlasEmitter : ISheetMetadataEmitter
{
    public string Id => "libgdx";
    public string DisplayName => "libGDX TexturePacker (.atlas)";
    public string FileExtension => ".atlas";

    public string Emit(PackedSheet sheet, SheetMetadataOptions options)
    {
        var info = sheet.Info;
        // Covering, not plain Resolve: an atlas region is the only way to name pixels here, so a frame no tag
        // covers would be permanently unreachable through findRegion.
        var animations = SheetAnimationGrouping.ResolveCovering(sheet);
        var sb = new StringBuilder();

        // Page header. Nearest filtering is the only correct choice for pixel art, and RGBA8888 matches the
        // PNG the builder writes.
        // The page name is the one field that must stay byte-identical to the real PNG, so it cannot be
        // sanitized the way region names are — but a line break would still split the header and make the
        // parser read `size:`/`format:` as region properties, so newlines are stripped regardless.
        sb.Append(CultureInfo.InvariantCulture, $"{StripNewlines(info.ImageFileName)}\n");
        sb.Append(CultureInfo.InvariantCulture, $"size: {sheet.Image.Width},{sheet.Image.Height}\n");
        sb.Append("format: RGBA8888\n");
        sb.Append("filter: Nearest,Nearest\n");
        sb.Append("repeat: none\n");

        // Grouping dedups tags by exact name, but Sanitize is lossy — "run" and "run:" both land on "run".
        // Two animations sharing a region name interleave under findRegions("run") with indexes 0,0,1,1...,
        // and libGDX's index sort then makes the play order arbitrary. Suffix collisions apart, the same way
        // the Unity preset does.
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var anim in animations)
        {
            var regionName = Sanitize(anim.NameOr(info.SpriteName));
            if (!usedNames.Add(regionName))
            {
                var suffix = 2;
                string candidate;
                do
                {
                    candidate = $"{regionName}_{suffix++}";
                } while (!usedNames.Add(candidate));

                regionName = candidate;
            }

            for (var i = 0; i < anim.Frames.Count; i++)
            {
                var frame = anim.Frames[i];

                // libGDX's `offset` is measured from the BOTTOM-left of the original frame, while our trim
                // rect is top-down — so the vertical offset is what is left below the trimmed content, not
                // above it. Getting this backwards shifts every trimmed frame by twice its top margin.
                var offsetX = frame.SpriteSourceRect.Left;
                var offsetY = frame.SourceSize.Height - frame.SpriteSourceRect.Top - frame.Frame.Height;

                sb.Append(CultureInfo.InvariantCulture, $"{regionName}\n");
                sb.Append("  rotate: false\n");
                sb.Append(CultureInfo.InvariantCulture, $"  xy: {frame.Frame.Left}, {frame.Frame.Top}\n");
                sb.Append(CultureInfo.InvariantCulture, $"  size: {frame.Frame.Width}, {frame.Frame.Height}\n");
                sb.Append(CultureInfo.InvariantCulture, $"  orig: {frame.SourceSize.Width}, {frame.SourceSize.Height}\n");
                sb.Append(CultureInfo.InvariantCulture, $"  offset: {offsetX}, {offsetY}\n");
                sb.Append(CultureInfo.InvariantCulture, $"  index: {i}\n");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Region names are newline-delimited in the descriptor and are looked up verbatim by
    /// <c>findRegion(name)</c>, so a name carrying a line break or a leading/trailing space would either
    /// break the parse or become unfindable.
    /// </summary>
    private static string Sanitize(string name)
    {
        var cleaned = StripNewlines(name).Replace(':', '_').Trim();
        return string.IsNullOrEmpty(cleaned) ? "sprite" : cleaned;
    }

    private static string StripNewlines(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
