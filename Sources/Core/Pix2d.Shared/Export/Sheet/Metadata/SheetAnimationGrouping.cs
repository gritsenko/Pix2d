#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// One named run of frames on the sheet, as the engine presets want it: a tag if the sprite has any,
/// otherwise a single implicit animation over every packed frame.
/// </summary>
/// <param name="Name">The tag name, or <c>null</c> when this is the implicit whole-sheet animation
/// (each emitter substitutes its own convention — Godot's <c>default</c>, libGDX's sprite name).</param>
/// <param name="Frames">The frames of this animation, in sheet order.</param>
/// <param name="Direction">Aseprite play direction (<c>forward</c> / <c>reverse</c> / <c>pingpong</c>).</param>
public sealed record SheetAnimation(string? Name, IReadOnlyList<PackedFrame> Frames, string Direction)
{
    public bool IsImplicit => Name is null;

    public string NameOr(string fallback) => Name ?? fallback;
}

/// <summary>
/// Turns a <see cref="PackedSheet"/>'s tag ranges into animations. Shared by every engine preset, because
/// all three answer the same question — "which frames belong to which animation" — and the answer has the
/// same three edge cases everywhere:
/// <list type="bullet">
/// <item>no tags at all → one implicit animation over the whole sheet (a plain single-animation export
/// must still produce something an engine can play);</item>
/// <item>duplicate tag names (Aseprite permits them, and so does our model) → unioned into one animation,
/// since every target here keys animations by name and would otherwise emit a broken duplicate key;</item>
/// <item>a tag range that covers no packed frame (possible after <see cref="SpriteSheetOptions.TagFilter"/>
/// re-bases the sheet) → dropped rather than emitted empty.</item>
/// </list>
/// Frame indices are compared in the sheet's own index space — <see cref="PackedFrame.Index"/> and
/// <see cref="SheetInfo.Tags"/> are already re-based together by the builder.
/// </summary>
public static class SheetAnimationGrouping
{
    public static IReadOnlyList<SheetAnimation> Resolve(PackedSheet sheet)
    {
        var tags = sheet.Info.Tags;
        if (tags.Count == 0)
            return [new SheetAnimation(null, sheet.Frames.ToArray(), "forward")];

        var result = new List<SheetAnimation>();
        foreach (var group in tags.GroupBy(t => t.Name, StringComparer.Ordinal))
        {
            var frames = sheet.Frames
                .Where(f => group.Any(t => f.Index >= t.From && f.Index <= t.To))
                .ToArray();

            if (frames.Length == 0)
                continue;

            result.Add(new SheetAnimation(group.Key, frames, group.First().Direction));
        }

        // Every tag was out of range — fall back to the whole sheet rather than emitting no animation,
        // which would make the sidecar useless while the PNG next to it is perfectly fine.
        if (result.Count == 0)
            return [new SheetAnimation(null, sheet.Frames.ToArray(), "forward")];

        return result;
    }

    /// <summary>
    /// <see cref="Resolve"/> plus a trailing implicit animation holding any packed frame no tag covers, so
    /// every frame in the PNG is addressable in the sidecar.
    /// <para>
    /// For formats whose unit is a plain region — Unity's sprite rects, libGDX's atlas regions — a frame with
    /// no entry is unreachable: Unity cannot slice it (and hand-slicing is overwritten on the next
    /// re-export), and libGDX's <c>findRegion</c> can never name those pixels. Partial tag coverage is
    /// ordinary (tag <c>run</c> = frames 0-3 of a 6-frame sprite leaves 4-5 uncovered), and the Aseprite JSON
    /// emitter already writes all frames regardless, so dropping them here made the presets disagree with the
    /// flagship format on identical input.
    /// </para>
    /// Godot deliberately uses <see cref="Resolve"/> instead: a SpriteFrames resource has no concept of a
    /// free-standing frame, so an orphan there would only be noise.
    /// </summary>
    public static IReadOnlyList<SheetAnimation> ResolveCovering(PackedSheet sheet)
    {
        var animations = Resolve(sheet);

        var covered = animations.SelectMany(a => a.Frames).Select(f => f.Index).ToHashSet();
        var uncovered = sheet.Frames.Where(f => !covered.Contains(f.Index)).ToArray();
        if (uncovered.Length == 0)
            return animations;

        return [.. animations, new SheetAnimation(null, uncovered, "forward")];
    }

    /// <summary>
    /// Deterministic 128-bit id derived from <paramref name="seed"/>, formatted as 32 lowercase hex chars
    /// (Unity asset GUID / sprite id shape). Deterministic on purpose: re-exporting the same sheet must
    /// produce the same ids, or Unity treats every re-export as a brand-new asset and silently breaks every
    /// scene reference to the old sprites. Two FNV-1a passes with different offset bases; not a hash for
    /// security, just a stable spread, so it deliberately avoids System.Security.Cryptography (which is not
    /// uniformly available across the heads this assembly ships in, browser-wasm included).
    /// </summary>
    public static string StableHex128(string seed)
    {
        const ulong prime = 1099511628211;
        var lo = 14695981039346656037UL;
        var hi = 1469598103934665603UL;

        foreach (var ch in seed)
        {
            lo = (lo ^ ch) * prime;
            hi = (hi ^ (ulong)(ch * 31 + 7)) * prime;
        }

        // Avalanche both halves so short, similar seeds ("hero 0" / "hero 1") don't share a long prefix.
        lo ^= lo >> 33; lo *= 0xff51afd7ed558ccdUL; lo ^= lo >> 33;
        hi ^= hi >> 33; hi *= 0xc4ceb9fe1a85ec53UL; hi ^= hi >> 33;

        return lo.ToString("x16") + hi.ToString("x16");
    }
}
