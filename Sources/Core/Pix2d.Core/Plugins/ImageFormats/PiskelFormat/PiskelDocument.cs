#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Pix2d.Plugins.ImageFormats.PiskelFormat;

/// <summary>
/// Pure reader for Piskel's <c>.piskel</c> document (roadmap H2.3) — JSON in, structure out, no IO and no
/// SkiaSharp, so the parsing rules are testable on their own.
///
/// The format is a small JSON envelope around per-layer sprite sheets:
/// <code>
/// { "modelVersion": 2,
///   "piskel": { "name": …, "fps": 12, "width": 32, "height": 32,
///               "layers": [ "{\"name\":\"Layer 1\",\"opacity\":1,\"frameCount\":3,\"chunks\":[…]}" ] } }
/// </code>
/// Two things about it routinely trip up readers, and both are handled here:
/// <list type="bullet">
/// <item><b><c>layers</c> holds JSON <i>strings</i>, not objects.</b> Each entry is a separately serialized
/// layer document that has to be parsed a second time. Piskel does this so a layer can be round-tripped on
/// its own; a reader that expects objects sees an array of opaque strings and gives up.</item>
/// <item><b>A chunk's image is a sheet, and <c>layout</c> is the frame→column mapping.</b> Each chunk carries
/// one horizontal strip of <c>width</c>-wide cells in <c>base64PNG</c> plus a <c>layout</c> array whose
/// <i>i</i>-th entry lists every frame index drawn by cell <i>i</i>. That indirection is how Piskel
/// deduplicates identical frames, so treating cell <i>i</i> as "frame <i>i</i>" silently scrambles the
/// animation of any sprite with a repeated frame.</item>
/// </list>
/// </summary>
public static class PiskelDocument
{
    /// <summary>The <c>modelVersion</c> this reader understands.</summary>
    public const int SupportedModelVersion = 2;

    /// <summary>
    /// Upper bound on a document's timeline length. Not a format limit — a sanity bound, because the frame
    /// count is read from the file and drives a per-frame allocation before anything else validates it. Set
    /// far above any plausible hand-drawn animation (the project's own largest test sprite is 232 frames).
    /// </summary>
    public const int MaxFrameCount = 8192;

    public sealed record Document(
        string Name,
        int Width,
        int Height,
        float Fps,
        int FrameCount,
        IReadOnlyList<Layer> Layers);

    public sealed record Layer(
        string Name,
        float Opacity,
        int FrameCount,
        IReadOnlyList<Chunk> Chunks);

    /// <param name="Layout">Per sheet cell, the frame indices that cell fills. <c>Layout[i]</c> ↔ cell <c>i</c>.</param>
    /// <param name="Base64Png">The chunk's sheet, as the raw base64 payload (any <c>data:</c> prefix stripped).</param>
    public sealed record Chunk(IReadOnlyList<IReadOnlyList<int>> Layout, string Base64Png);

    public static Document Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("Not a .piskel file: the file is empty.");

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (Exception ex)
        {
            throw new FormatException("Not a .piskel file: the content is not valid JSON. " + ex.Message);
        }

        var piskel = root["piskel"] as JObject
                     ?? throw new FormatException("Not a .piskel file: no \"piskel\" object at the root.");

        // Absent modelVersion is treated as the current one: some third-party writers omit it, and every
        // file seen in the wild without it uses the v2 shape. A *known-different* version is rejected
        // instead of guessed at, so a v1 file fails with a message rather than importing as garbage.
        var modelVersion = (int?)root["modelVersion"] ?? SupportedModelVersion;
        if (modelVersion != SupportedModelVersion)
            throw new FormatException(
                $"Unsupported .piskel model version {modelVersion} (this build reads version {SupportedModelVersion}). " +
                "Re-save the file with a current version of Piskel.");

        var width = (int?)piskel["width"] ?? 0;
        var height = (int?)piskel["height"] ?? 0;
        if (width <= 0 || height <= 0)
            throw new FormatException($"Invalid .piskel canvas size {width}x{height}.");

        // Reject oversize canvases here rather than downstream. The importer allocates a full-canvas bitmap
        // per frame *before* anything clamps, so a declared 20000x20000 eagerly burns ~1.6 GB per frame and
        // then fails anyway: Pix2dSprite.CreateEmpty clamps the sprite to MaxDimension while the import data
        // keeps the original size, and InsertFrameFromBitmap throws on the mismatch. Failing up front costs
        // nothing and says why.
        if (CanvasSize.IsOversized(width, height))
            throw new FormatException(
                $"The .piskel canvas is {width}x{height}, larger than the maximum supported size of " +
                $"{(int)CanvasSize.MaxDimension}x{(int)CanvasSize.MaxDimension}.");

        var layers = new List<Layer>();
        if (piskel["layers"] is JArray layerArray)
        {
            for (var i = 0; i < layerArray.Count; i++)
                layers.Add(ParseLayer(layerArray[i], i));
        }

        if (layers.Count == 0)
            throw new FormatException("The .piskel file has no layers.");

        // Layers are allowed to disagree; the sprite's timeline is as long as the longest one.
        var frameCount = Math.Max(1, layers.Max(l => l.FrameCount));

        // Both inputs to that number are attacker-controlled and unrelated to the file's size: "frameCount":
        // 2000000000, or a single layout entry [[1999999999]], each make the importer allocate an
        // SKBitmap?[2000000000] (a 16 GB reference array) and then try to synthesize an empty bitmap per
        // uncovered frame. That ends in OutOfMemoryException on desktop and can take the process down
        // outright on Android/WASM, so it is bounded here where the claim is first read.
        if (frameCount > MaxFrameCount)
            throw new FormatException(
                $"The .piskel file declares {frameCount} frames, more than the maximum supported {MaxFrameCount}.");

        return new Document(
            Name: (string?)piskel["name"] is { Length: > 0 } n ? n : "Piskel",
            Width: width,
            Height: height,
            // Piskel's default is 12 fps; a zero/absent value would otherwise reach Pix2dSprite.FrameRate,
            // where the timeline divides by it.
            Fps: (float?)piskel["fps"] is > 0 and { } fps ? fps : 12f,
            FrameCount: frameCount,
            Layers: layers);
    }

    private static Layer ParseLayer(JToken token, int index)
    {
        // The double encoding: a layer is a JSON string holding a JSON object. Newer writers sometimes
        // inline the object directly, so accept both rather than insisting on the string form.
        JObject layerObj;
        if (token.Type == JTokenType.String)
        {
            var text = (string?)token ?? "";
            try
            {
                layerObj = JObject.Parse(text);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Layer {index} is not valid JSON. " + ex.Message);
            }
        }
        else if (token is JObject direct)
        {
            layerObj = direct;
        }
        else
        {
            throw new FormatException($"Layer {index} has an unexpected shape ({token.Type}).");
        }

        var chunks = new List<Chunk>();
        if (layerObj["chunks"] is JArray chunkArray)
        {
            foreach (var chunkToken in chunkArray)
            {
                if (chunkToken is not JObject chunk)
                    continue;

                var base64 = (string?)chunk["base64PNG"] ?? "";
                if (string.IsNullOrWhiteSpace(base64))
                    continue;

                chunks.Add(new Chunk(ParseLayout(chunk["layout"]), StripDataUri(base64)));
            }
        }

        var declaredFrames = (int?)layerObj["frameCount"] ?? 0;

        // frameCount is advisory — trust whichever is larger so a layout referencing frame 5 is never
        // truncated by a stale count (and an absent count still yields a usable layer).
        var layoutFrames = chunks
            .SelectMany(c => c.Layout)
            .SelectMany(cell => cell)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        return new Layer(
            Name: (string?)layerObj["name"] is { Length: > 0 } name ? name : $"Layer {index + 1}",
            Opacity: (float?)layerObj["opacity"] ?? 1f,
            FrameCount: Math.Max(declaredFrames, layoutFrames),
            Chunks: chunks);
    }

    private static IReadOnlyList<IReadOnlyList<int>> ParseLayout(JToken? layout)
    {
        if (layout is not JArray outer)
            return [];

        var result = new List<IReadOnlyList<int>>(outer.Count);
        foreach (var cell in outer)
        {
            // Normally [[0],[1],[2]]; tolerate a bare number for a single-frame cell.
            if (cell is JArray inner)
                result.Add(inner.Select(v => (int?)v ?? 0).ToArray());
            else if (cell.Type is JTokenType.Integer or JTokenType.Float)
                result.Add([(int)cell]);
            else
                result.Add([]);
        }

        return result;
    }

    /// <summary>Piskel writes <c>data:image/png;base64,…</c>; Convert.FromBase64String needs the payload alone.</summary>
    private static string StripDataUri(string value)
    {
        var comma = value.IndexOf(',');
        return value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0
            ? value[(comma + 1)..]
            : value;
    }
}
