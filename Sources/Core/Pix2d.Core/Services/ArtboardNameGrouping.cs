#nullable enable
using Pix2d.CommonNodes;
using SkiaNodes.Extensions;

namespace Pix2d.Services;

/// <summary>
/// Name-based grouping used by <see cref="EditService.ArrangeSelectedObjects"/>: artboard names in real
/// projects are prefixed families ("icon-goal-gem", "icon-goal-ice", "icon-star-empty"), so arranging by
/// those prefixes keeps a family together on the canvas instead of packing the selection blindly.
/// </summary>
internal static class ArtboardNameGrouping
{
    // Every separator commonly used in sprite/asset names; camelCase is deliberately NOT split, since
    // "IconGoal" families are rare next to kebab/snake ones and word-splitting them is guesswork.
    private static readonly char[] NameSeparators = ['-', '_', ' ', '.', '/'];

    /// <summary>
    /// Splits <paramref name="sprites"/> into layout groups: each artboard joins the group of the deepest
    /// name prefix it shares with at least one other artboard in the set. With icon-goal-gem /
    /// icon-goal-ice / icon-star-empty / hero selected the groups are "icon-goal" (2 members), "icon"
    /// (just icon-star-empty — no other selected icon-star* to pair with) and a trailing prefix-less
    /// bucket ("hero", plus anything unnamed).
    ///
    /// Groups come back in layout order — alphabetically by their first member, prefix-less bucket last —
    /// with members in natural name order ("frame 2" before "frame 10"), canvas reading order breaking
    /// ties so equal / missing names still arrange deterministically.
    /// </summary>
    public static IReadOnlyList<Pix2dSprite[]> Group(IReadOnlyList<Pix2dSprite> sprites)
    {
        var segments = sprites
            .Select(s => (s.Name ?? string.Empty).Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        // How many of these artboards each prefix covers: "icon" -> 4, "icon-goal" -> 2, "icon-goal-gem" -> 1.
        var shared = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var segs in segments)
            for (var length = 1; length <= segs.Length; length++)
            {
                var prefix = Prefix(segs, length);
                shared[prefix] = shared.GetValueOrDefault(prefix) + 1;
            }

        return sprites
            .Select((sprite, i) => (Sprite: sprite, Key: DeepestSharedPrefix(segments[i], shared)))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                g.Key,
                Members = g.Select(x => x.Sprite)
                    .OrderBy(x => x.Name ?? string.Empty, NaturalName)
                    .ThenBy(x => x.GetBoundingBox().Top)
                    .ThenBy(x => x.GetBoundingBox().Left)
                    .ToArray()
            })
            .OrderBy(g => g.Key.Length == 0) // false sorts first: the prefix-less bucket goes last
            .ThenBy(g => g.Members[0].Name ?? string.Empty, NaturalName)
            .Select(g => g.Members)
            .ToArray();
    }

    private static string Prefix(string[] segments, int length) => string.Join('-', segments, 0, length);

    /// <summary>
    /// The longest prefix of <paramref name="segments"/> that at least two artboards of the set share, or
    /// <see cref="string.Empty"/> when the name shares none. Counts only shrink as the prefix grows, so
    /// the first unshared length ends the walk.
    /// </summary>
    private static string DeepestSharedPrefix(string[] segments, Dictionary<string, int> shared)
    {
        var key = string.Empty;

        for (var length = 1; length <= segments.Length; length++)
        {
            var prefix = Prefix(segments, length);
            if (shared[prefix] < 2)
                break;

            key = prefix;
        }

        return key;
    }

    /// <summary>
    /// Case-insensitive name order with digit runs compared by value, so "frame 2" precedes "frame 10"
    /// (plain ordinal order puts "frame 10" first, which reads as broken on numbered sprite sets).
    /// </summary>
    private static readonly IComparer<string> NaturalName = Comparer<string>.Create(static (a, b) =>
    {
        int i = 0, j = 0;

        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var endA = i;
                while (endA < a.Length && char.IsDigit(a[endA])) endA++;
                var endB = j;
                while (endB < b.Length && char.IsDigit(b[endB])) endB++;

                // Leading zeros carry no value, so the longer run is the bigger number after trimming them.
                var runA = a.AsSpan(i, endA - i).TrimStart('0');
                var runB = b.AsSpan(j, endB - j).TrimStart('0');
                if (runA.Length != runB.Length)
                    return runA.Length - runB.Length;

                var digits = runA.CompareTo(runB, StringComparison.Ordinal);
                if (digits != 0)
                    return digits;

                i = endA;
                j = endB;
                continue;
            }

            var chars = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (chars != 0)
                return chars;

            i++;
            j++;
        }

        // Whichever name still has characters left is the longer one.
        return (a.Length - i) - (b.Length - j);
    });
}
