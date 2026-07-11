#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Registry of available metadata emitters. Preset formats (Godot/Unity/libGDX) register here as they
/// land, and both the export settings dropdown and the CLI <c>--format</c> flag enumerate the same set.
/// </summary>
public static class SheetMetadataEmitters
{
    /// <summary>Emitter id meaning "image only, no sidecar".</summary>
    public const string None = "none";

    private static readonly List<ISheetMetadataEmitter> _emitters =
    [
        new AsepriteJsonEmitter()
    ];

    public static IReadOnlyList<ISheetMetadataEmitter> All => _emitters;

    public static ISheetMetadataEmitter? TryGet(string? id) =>
        string.IsNullOrWhiteSpace(id) || string.Equals(id, None, StringComparison.OrdinalIgnoreCase)
            ? null
            : _emitters.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}
