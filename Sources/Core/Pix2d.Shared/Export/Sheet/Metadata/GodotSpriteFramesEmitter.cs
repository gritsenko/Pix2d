#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Emits a Godot 4 <c>SpriteFrames</c> resource (<c>.tres</c>) referencing the exported sheet PNG through
/// one <c>AtlasTexture</c> sub-resource per frame. Dropped next to the PNG inside a Godot project it can be
/// assigned straight to an <c>AnimatedSprite2D</c> — no import step, no addon.
///
/// Mapping decisions worth knowing:
/// <list type="bullet">
/// <item><b>Texture path.</b> The emitter cannot know where in the project tree the user will put the PNG,
/// so the <c>ext_resource</c> points at <c>res://&lt;image file name&gt;</c> — i.e. correct when the pair
/// lands at the project root, and a one-line fix otherwise (this is what every Aseprite→Godot converter
/// does). Godot reports a missing dependency rather than failing silently, and its "Fix Dependencies"
/// dialog re-points it.</item>
/// <item><b>Trim.</b> A trimmed frame keeps its original footprint via <c>AtlasTexture.margin</c>: position =
/// the trim offset, size = the pixels cropped away. Without it a trimmed export would shift frames around
/// their own pivot on every frame change.</item>
/// <item><b>Per-frame duration.</b> Godot stores a frame's duration as a <i>multiplier</i> of the animation
/// speed, not as time — so a frame is <c>durationMs × fps / 1000</c>, which is exactly 1.0 for a frame
/// running at the sprite's own frame rate.</item>
/// <item><b>Direction.</b> <c>SpriteFrames</c> has a <c>loop</c> flag and nothing else, so reverse and
/// pingpong tags export as ordinary looping animations; the frame list is unchanged.</item>
/// </list>
/// </summary>
public sealed class GodotSpriteFramesEmitter : ISheetMetadataEmitter
{
    public string Id => "godot";
    public string DisplayName => "Godot 4 SpriteFrames (.tres)";
    public string FileExtension => ".tres";

    /// <summary>Default animation name Godot's own AnimatedSprite2D looks for.</summary>
    private const string DefaultAnimationName = "default";

    public string Emit(PackedSheet sheet, SheetMetadataOptions options)
    {
        var info = sheet.Info;
        var animations = SheetAnimationGrouping.Resolve(sheet);

        // Only frames an animation actually references get an AtlasTexture: under a tag filter the sheet can
        // legitimately hold frames no animation plays, and an orphan sub-resource is pure noise in the file.
        var referenced = animations
            .SelectMany(a => a.Frames)
            .Select(f => f.Index)
            .Distinct()
            .OrderBy(i => i)
            .ToArray();

        var textureId = $"1_{SheetAnimationGrouping.StableHex128(info.ImageFileName)[..5]}";
        // The frame index leads the id so it is unique *by construction*. A bare 5-hex-char hash is only a
        // 20-bit space: at 300 frames two frames collide with ~4% probability, and Godot's loader keeps the
        // last [sub_resource] with a given id, so both references would silently resolve to the same region
        // and two animation frames would show identical pixels. The hash suffix is kept so ids stay stable
        // per sprite across re-exports.
        var subIds = referenced.ToDictionary(i => i, i => $"AtlasTexture_{i}_{SheetAnimationGrouping.StableHex128(info.SpriteName + "#" + i)[..5]}");

        var sb = new StringBuilder();

        // load_steps counts every resource the file loads (1 ext + N sub) plus the resource itself.
        sb.Append(CultureInfo.InvariantCulture, $"[gd_resource type=\"SpriteFrames\" load_steps={referenced.Length + 2} format=3]\n\n");
        sb.Append(CultureInfo.InvariantCulture, $"[ext_resource type=\"Texture2D\" path=\"res://{Escape(info.ImageFileName)}\" id=\"{textureId}\"]\n\n");

        foreach (var index in referenced)
        {
            var frame = sheet.Frames.First(f => f.Index == index);
            sb.Append(CultureInfo.InvariantCulture, $"[sub_resource type=\"AtlasTexture\" id=\"{subIds[index]}\"]\n");
            sb.Append(CultureInfo.InvariantCulture, $"atlas = ExtResource(\"{textureId}\")\n");
            sb.Append(CultureInfo.InvariantCulture,
                $"region = Rect2({frame.Frame.Left}, {frame.Frame.Top}, {frame.Frame.Width}, {frame.Frame.Height})\n");

            if (frame.Trimmed)
            {
                var marginW = frame.SourceSize.Width - frame.Frame.Width;
                var marginH = frame.SourceSize.Height - frame.Frame.Height;
                sb.Append(CultureInfo.InvariantCulture,
                    $"margin = Rect2({frame.SpriteSourceRect.Left}, {frame.SpriteSourceRect.Top}, {marginW}, {marginH})\n");
            }

            sb.Append('\n');
        }

        var fps = info.FrameRate > 0 ? info.FrameRate : 10f;

        sb.Append("[resource]\n");
        sb.Append("animations = [");

        for (var a = 0; a < animations.Count; a++)
        {
            var anim = animations[a];
            if (a > 0) sb.Append(", ");

            sb.Append("{\n\"frames\": [");
            for (var f = 0; f < anim.Frames.Count; f++)
            {
                var frame = anim.Frames[f];
                if (f > 0) sb.Append(", ");
                sb.Append(CultureInfo.InvariantCulture,
                    $"{{\n\"duration\": {FormatDuration(frame.DurationMs, fps)},\n\"texture\": SubResource(\"{subIds[frame.Index]}\")\n}}");
            }

            sb.Append(CultureInfo.InvariantCulture,
                $"],\n\"loop\": true,\n\"name\": &\"{Escape(anim.NameOr(DefaultAnimationName))}\",\n\"speed\": {Num(fps)}\n}}");

        }

        sb.Append("]\n");
        return sb.ToString();
    }

    /// <summary>
    /// Godot's per-frame <c>duration</c> is relative to the animation speed: 1.0 means "one tick at
    /// <paramref name="fps"/>". A sprite with no per-frame overrides therefore emits exactly 1.0 for every
    /// frame — which needs the explicit snap below, because the sprite's own default duration is stored as
    /// whole milliseconds (15 fps → 67 ms, not 66.67), so computing the ratio naively yields 1.005 and would
    /// play the animation 0.5 % slow for no reason. Anything genuinely overridden falls through and keeps its
    /// real ratio.
    /// </summary>
    private static string FormatDuration(int durationMs, float fps)
    {
        var defaultMs = (int)System.Math.Round(1000f / fps);
        if (durationMs == defaultMs)
            return "1.0";

        var relative = durationMs * fps / 1000f;
        if (relative <= 0) relative = 1f;
        return Num(relative);
    }

    private static string Num(float value) =>
        value == (int)value
            ? ((int)value).ToString(CultureInfo.InvariantCulture) + ".0"
            : value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Godot's text resource format is quoted-string based; only quotes and backslashes need care.</summary>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
