#nullable enable
namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Serialises a <see cref="PackedSheet"/>'s geometry + animation info into an engine-consumable text
/// sidecar. One implementation per target format (Aseprite JSON now; Godot/Unity/libGDX later), all
/// consuming the same packing result — so a new preset never touches the packer or the renderer.
/// </summary>
public interface ISheetMetadataEmitter
{
    /// <summary>Stable id used on the CLI (<c>--format</c>) and to select the emitter in the UI.</summary>
    string Id { get; }

    /// <summary>Human-readable name for the export settings dropdown.</summary>
    string DisplayName { get; }

    /// <summary>Sidecar file extension including the dot (e.g. ".json").</summary>
    string FileExtension { get; }

    /// <summary>Produces the sidecar text for the given packed sheet.</summary>
    string Emit(PackedSheet sheet, SheetMetadataOptions options);
}

/// <summary>Emitter options shared across formats.</summary>
public sealed record SheetMetadataOptions
{
    /// <summary>Written into <c>meta.version</c> (the running Pix2D version).</summary>
    public string AppVersion { get; init; } = "";

    /// <summary>Emit <c>frames</c> as a JSON array (Aseprite <c>--format json-array</c>) instead of a hash.</summary>
    public bool ArrayFrames { get; init; } = false;

    /// <summary>Include the <c>meta.layers</c> section.</summary>
    public bool IncludeLayers { get; init; } = true;

    /// <summary>Include the top-level Pixi/Phaser-style <c>animations</c> map (tag → frame keys).</summary>
    public bool IncludeAnimationsMap { get; init; } = true;
}
