#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pix2d.Export.Sheet.Metadata;

// DTOs mirroring Aseprite's `--data` JSON exactly (property names/casing are the contract). Populated
// by AsepriteJsonEmitter and serialised with Newtonsoft. Null members are dropped on write, so optional
// blocks (slices, layers, animations, slice center/pivot) only appear when they carry data.

public sealed class AseRect
{
    [JsonProperty("x")] public int X { get; set; }
    [JsonProperty("y")] public int Y { get; set; }
    [JsonProperty("w")] public int W { get; set; }
    [JsonProperty("h")] public int H { get; set; }
}

public sealed class AseSize
{
    [JsonProperty("w")] public int W { get; set; }
    [JsonProperty("h")] public int H { get; set; }
}

public sealed class AsePoint
{
    [JsonProperty("x")] public int X { get; set; }
    [JsonProperty("y")] public int Y { get; set; }
}

public sealed class AseFrame
{
    // Present only in json-array mode; omitted (null) in the hash form where the key carries the name.
    [JsonProperty("filename", NullValueHandling = NullValueHandling.Ignore)]
    public string? Filename { get; set; }

    [JsonProperty("frame")] public AseRect Frame { get; set; } = new();
    [JsonProperty("rotated")] public bool Rotated { get; set; }
    [JsonProperty("trimmed")] public bool Trimmed { get; set; }
    [JsonProperty("spriteSourceSize")] public AseRect SpriteSourceSize { get; set; } = new();
    [JsonProperty("sourceSize")] public AseSize SourceSize { get; set; } = new();
    [JsonProperty("duration")] public int Duration { get; set; }
}

public sealed class AseTag
{
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("from")] public int From { get; set; }
    [JsonProperty("to")] public int To { get; set; }
    [JsonProperty("direction")] public string Direction { get; set; } = "forward";

    [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
    public string? Color { get; set; }
}

public sealed class AseLayer
{
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("opacity")] public int Opacity { get; set; }
    [JsonProperty("blendMode")] public string BlendMode { get; set; } = "normal";
}

public sealed class AseSliceKey
{
    [JsonProperty("frame")] public int Frame { get; set; }
    [JsonProperty("bounds")] public AseRect Bounds { get; set; } = new();

    [JsonProperty("center", NullValueHandling = NullValueHandling.Ignore)]
    public AseRect? Center { get; set; }

    [JsonProperty("pivot", NullValueHandling = NullValueHandling.Ignore)]
    public AsePoint? Pivot { get; set; }
}

public sealed class AseSlice
{
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("color")] public string Color { get; set; } = "#0000ffff";
    [JsonProperty("keys")] public List<AseSliceKey> Keys { get; set; } = new();
}

public sealed class AseMeta
{
    [JsonProperty("app")] public string App { get; set; } = "https://pix2d.com/";
    [JsonProperty("version")] public string Version { get; set; } = "";
    [JsonProperty("image")] public string Image { get; set; } = "";
    [JsonProperty("format")] public string Format { get; set; } = "RGBA8888";
    [JsonProperty("size")] public AseSize Size { get; set; } = new();

    // Aseprite writes scale as a string ("1"); strict parsers depend on it.
    [JsonProperty("scale")] public string Scale { get; set; } = "1";

    [JsonProperty("frameTags")] public List<AseTag> FrameTags { get; set; } = new();

    [JsonProperty("layers", NullValueHandling = NullValueHandling.Ignore)]
    public List<AseLayer>? Layers { get; set; }

    [JsonProperty("slices")] public List<AseSlice> Slices { get; set; } = new();
}

public sealed class AseDocument
{
    // Dictionary<string, AseFrame> (hash form) or List<AseFrame> (json-array form).
    [JsonProperty("frames")] public object Frames { get; set; } = new Dictionary<string, AseFrame>();

    [JsonProperty("meta")] public AseMeta Meta { get; set; } = new();

    [JsonProperty("animations", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string[]>? Animations { get; set; }
}
