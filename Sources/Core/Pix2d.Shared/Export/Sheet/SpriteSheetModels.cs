#nullable enable
using System.Collections.Generic;
using SkiaSharp;

namespace Pix2d.Export.Sheet;

/// <summary>How frames are laid out on the sheet.</summary>
public enum SheetPackMode
{
    /// <summary>Uniform grid, one cell per frame, <see cref="SpriteSheetOptions.MaxColumns"/> wide.</summary>
    Grid,

    /// <summary>Shelf-packed (frames may have different sizes when trimmed); minimises wasted space.</summary>
    Tight
}

/// <summary>
/// Immutable options driving a sprite-sheet build. Pure data — no Avalonia, no services — so the same
/// options flow through the in-app exporter and the headless CLI unchanged.
/// </summary>
public sealed record SpriteSheetOptions
{
    public SheetPackMode PackMode { get; init; } = SheetPackMode.Grid;

    /// <summary>Columns in <see cref="SheetPackMode.Grid"/> mode (ignored for Tight). Clamped to ≥ 1.</summary>
    public int MaxColumns { get; init; } = 4;

    /// <summary>Transparent gutter, in output pixels, between adjacent frames.</summary>
    public int Padding { get; init; } = 0;

    /// <summary>Crop each frame to its opaque bounding box; the trim offset is recorded in the metadata.</summary>
    public bool Trim { get; init; }

    /// <summary>Round the final sheet width/height up to the next power of two.</summary>
    public bool PowerOfTwo { get; init; }

    /// <summary>Export only the frame range of this animation tag (when the sprite has one). Null = all frames.</summary>
    public string? TagFilter { get; init; }

    /// <summary>Base name used for frame keys in metadata (e.g. "hero" → "hero 0").</summary>
    public string SpriteName { get; init; } = "sprite";

    /// <summary>The image file name written into <c>meta.image</c> (e.g. "hero.png").</summary>
    public string ImageFileName { get; init; } = "sprite.png";
}

/// <summary>One frame's placement on the sheet plus its trim/source geometry (all in output pixels).</summary>
public sealed record PackedFrame(
    // Index within the EXPORTED sheet (0-based over the frames actually packed), not the source frame
    // index in the sprite. The two differ only under SpriteSheetOptions.TagFilter, where the sheet is
    // re-based to 0 like Aseprite's own --tag export; SheetInfo.Tags is re-based to match, so the
    // metadata emitters can pair frames against tag ranges in one index space.
    int Index,
    SKRectI Frame,           // placement rect on the sheet
    bool Rotated,            // always false in v1 (frame rotation not supported yet)
    bool Trimmed,
    SKRectI SpriteSourceRect,// trimmed content rect within the original (scaled) frame: {x,y} = offset
    SKSizeI SourceSize,      // full original (scaled) frame size
    int DurationMs);

/// <summary>A packed sheet: the composited image plus per-frame geometry and animation info.</summary>
public sealed class PackedSheet : System.IDisposable
{
    public required SKBitmap Image { get; init; }
    public required IReadOnlyList<PackedFrame> Frames { get; init; }
    public required SheetInfo Info { get; init; }

    public void Dispose() => Image.Dispose();
}

/// <summary>Document-level info about the sheet, consumed by every metadata emitter.</summary>
public sealed record SheetInfo(
    string SpriteName,
    string ImageFileName,
    SKSizeI CanvasSize,      // scaled source-frame size
    double Scale,
    float FrameRate,
    IReadOnlyList<SheetTagInfo> Tags,
    IReadOnlyList<SheetLayerInfo> Layers,
    SKPointI? Pivot,         // canvas-space anchor, scaled; null = unset
    NineSliceInfo? NineSlice);

/// <summary>A named animation range within the sheet (Aseprite frameTag).</summary>
public sealed record SheetTagInfo(string Name, int From, int To, string Direction, string? Color);

/// <summary>Layer summary for <c>meta.layers</c>.</summary>
public sealed record SheetLayerInfo(string Name, int Opacity, string BlendMode);

/// <summary>9-slice margins (scaled pixels) mapped to an Aseprite slice <c>center</c> rect.</summary>
public sealed record NineSliceInfo(int Left, int Top, int Right, int Bottom);
