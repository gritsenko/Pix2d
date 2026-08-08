#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Registry of available metadata emitters. Both the export settings dropdown and the CLI <c>--format</c>
/// flag enumerate this same list, so registering an emitter here is all it takes to surface it in the app
/// and on the command line — no UI or CLI change required.
///
/// Order is the UI order; Aseprite JSON stays first because it is the default and the most portable
/// (engine-agnostic importers are written against it), followed by the direct engine presets.
/// </summary>
public static class SheetMetadataEmitters
{
    /// <summary>Emitter id meaning "image only, no sidecar".</summary>
    public const string None = "none";

    private static readonly List<ISheetMetadataEmitter> _emitters =
    [
        new AsepriteJsonEmitter(),
        new GodotSpriteFramesEmitter(),
        new UnityMetaEmitter(),
        new LibGdxAtlasEmitter()
    ];

    public static IReadOnlyList<ISheetMetadataEmitter> All => _emitters;

    public static ISheetMetadataEmitter? TryGet(string? id) =>
        string.IsNullOrWhiteSpace(id) || string.Equals(id, None, StringComparison.OrdinalIgnoreCase)
            ? null
            : _emitters.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}
