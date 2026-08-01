using Pix2d.Abstract.Drawing;

namespace Pix2d.Plugins.Drawing.Brushes;

/// <summary>
/// The on-disk identity of a brush type. A saved preset stores one of these keys instead of a CLR type name,
/// so renaming or moving a brush class does not orphan every preset the user saved — the same discipline
/// <c>NodeTypeRegistry</c> applies to persisted node types, minus the machinery (there are four brushes).
///
/// <para><b>Never change a key once shipped.</b> Resolution is tolerant in both directions: an unknown key
/// yields null and the caller skips that preset, and a brush with no registered key is simply not saveable
/// rather than being written under a name that won't come back.</para>
/// </summary>
public static class BrushKeys
{
    /// <summary>
    /// On-disk brush key for an <see cref="ImageStampBrush"/> preset. Unlike every key in <see cref="Map"/>,
    /// a stamp is never resolved through <see cref="Resolve"/> against a shared singleton — each stamp preset
    /// owns its own captured bitmap, so <c>DrawingService</c> reconstructs a fresh instance from the persisted
    /// image data instead. Kept here anyway because it is still "the on-disk identity of a brush type", just
    /// with a different reconstruction path.
    /// </summary>
    public const string StampKey = "stamp";

    private static readonly (string Key, Type Type)[] Map =
    [
        ("square", typeof(SquareSolidBrush)),
        ("circle", typeof(CircleSolidBrush)),
        ("spray", typeof(SprayBrush)),
        ("marker", typeof(MarkerBrush)),
    ];

    /// <summary>The stable key for a brush instance, or null when its type has none registered.</summary>
    public static string? GetKey(IPixelBrush? brush)
    {
        if (brush == null)
            return null;

        var type = brush.GetType();
        foreach (var (key, mapped) in Map)
        {
            if (mapped == type)
                return key;
        }

        return null;
    }

    /// <summary>
    /// Resolves a stored key against the live brush instances (which are shared singletons — the caller must
    /// use the returned instance, never construct its own). Null when the key is unknown.
    /// </summary>
    public static IPixelBrush? Resolve(string? key, IEnumerable<IPixelBrush> availableBrushes)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        foreach (var (mappedKey, type) in Map)
        {
            if (!string.Equals(mappedKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var brush in availableBrushes)
            {
                if (brush.GetType() == type)
                    return brush;
            }
        }

        return null;
    }
}
