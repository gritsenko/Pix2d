#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Emits the Unity importer sidecar for the exported sheet — a <c>&lt;image&gt;.png.meta</c> asset-database
/// file with <c>spriteMode: Multiple</c> and one pre-sliced sprite per frame. Copying the PNG + this file
/// into <c>Assets/</c> gives a sheet that is already sliced, already point-filtered and already uncompressed,
/// which is the whole manual chore of getting pixel art into Unity.
///
/// Unlike the other presets this is not a description of the sheet, it *is* Unity's own serialized importer
/// state, so several conventions are non-negotiable:
/// <list type="bullet">
/// <item><b>Y is flipped.</b> Unity sprite rects are bottom-up in texture space while the packer works
/// top-down, so every rect's <c>y</c> is <c>sheetHeight − (top + height)</c>. Skipping this mirrors the whole
/// sheet vertically and is the classic way these files come out wrong.</item>
/// <item><b>Ids must be stable.</b> <c>guid</c>, <c>spriteID</c> and <c>internalID</c> are what scene and
/// prefab references point at. They are derived deterministically from the sprite/frame name, so
/// re-exporting over an existing asset keeps every reference intact; a random id per export would silently
/// turn every referencing object's sprite into "None".</item>
/// <item><b>Pixel-art defaults.</b> <c>filterMode: 0</c> (Point), <c>textureCompression: 0</c> (Uncompressed),
/// <c>mipmapEnabled: 0</c>, <c>spriteMeshType: 0</c> (FullRect — also required for 9-slice borders to be
/// honoured) and <c>wrapMode: 1</c> (Clamp).</item>
/// <item><b>9-slice.</b> Unity's <c>border</c> is (left, bottom, right, top); our margins are top-down, so
/// <c>y</c> takes the bottom margin and <c>w</c> the top one.</item>
/// </list>
///
/// Not representable, by design of the format: animation timing. A <c>.meta</c> describes the texture, not
/// clips — so tags survive only as the sprite <i>naming</i> (<c>run_0</c>, <c>run_1</c>, …), which is what
/// makes them selectable as an ordered set when building an Animation clip. Use the Aseprite JSON preset
/// alongside it if a tool needs the durations.
/// </summary>
public sealed class UnityMetaEmitter : ISheetMetadataEmitter
{
    public string Id => "unity";
    public string DisplayName => "Unity texture meta (.png.meta)";

    /// <summary>
    /// Deliberately a double extension: Unity's sidecar is <c>&lt;asset file name&gt;.meta</c>, i.e. it keeps
    /// the image's own <c>.png</c>. Both write paths compose it correctly — the picker path via
    /// <c>Path.ChangeExtension</c> and the batch path by appending to the base name.
    /// </summary>
    public string FileExtension => ".png.meta";

    /// <summary>Unity's YAML class id for a Sprite sub-asset, used by the internal-id name table.</summary>
    private const int SpriteClassId = 213;

    public string Emit(PackedSheet sheet, SheetMetadataOptions options)
    {
        var info = sheet.Info;
        var sprites = BuildSprites(sheet);

        var sb = new StringBuilder();
        sb.Append("fileFormatVersion: 2\n");
        sb.Append(CultureInfo.InvariantCulture, $"guid: {SheetAnimationGrouping.StableHex128("pix2d/texture/" + info.ImageFileName)}\n");
        sb.Append("TextureImporter:\n");
        sb.Append("  internalIDToNameTable:\n");

        foreach (var s in sprites)
        {
            sb.Append("  - first:\n");
            sb.Append(CultureInfo.InvariantCulture, $"      {SpriteClassId}: {s.InternalId}\n");
            sb.Append(CultureInfo.InvariantCulture, $"    second: {s.Name}\n");
        }

        sb.Append("  externalObjects: {}\n");
        sb.Append("  serializedVersion: 12\n");
        sb.Append("  mipmaps:\n");
        sb.Append("    mipMapMode: 0\n");
        sb.Append("    enableMipMap: 0\n");
        sb.Append("    borderMipMap: 0\n");
        sb.Append("    mipMapsPreserveCoverage: 0\n");
        sb.Append("    alphaTestReferenceValue: 0.5\n");
        sb.Append("    mipMapFadeDistanceStart: 1\n");
        sb.Append("    mipMapFadeDistanceEnd: 3\n");
        sb.Append("  bumpmap:\n");
        sb.Append("    convertToNormalMap: 0\n");
        sb.Append("    externalNormalMap: 0\n");
        sb.Append("    heightScale: 0.25\n");
        sb.Append("    normalMapFilter: 0\n");
        sb.Append("  isReadable: 0\n");
        sb.Append("  streamingMipmaps: 0\n");
        sb.Append("  streamingMipmapsPriority: 0\n");
        sb.Append("  grayScaleToAlpha: 0\n");
        sb.Append("  generateCubemap: 6\n");
        sb.Append("  cubemapConvolution: 0\n");
        sb.Append("  seamlessCubemap: 0\n");
        sb.Append("  textureFormat: 1\n");
        sb.Append("  maxTextureSize: 2048\n");
        sb.Append("  textureSettings:\n");
        sb.Append("    serializedVersion: 2\n");
        // Point filtering + clamp: the two settings that decide whether pixel art looks like pixel art.
        sb.Append("    filterMode: 0\n");
        sb.Append("    aniso: 1\n");
        sb.Append("    mipBias: 0\n");
        sb.Append("    wrapU: 1\n");
        sb.Append("    wrapV: 1\n");
        sb.Append("    wrapW: 1\n");
        sb.Append("  nPOTScale: 0\n");
        sb.Append("  lightmap: 0\n");
        sb.Append("  compressionQuality: 50\n");
        // spriteMode 2 = Multiple, which is what makes the sprites list below take effect.
        sb.Append("  spriteMode: 2\n");
        sb.Append("  spriteExtrude: 1\n");
        sb.Append("  spriteMeshType: 0\n");
        sb.Append("  alignment: 0\n");
        sb.Append("  spritePivot: {x: 0.5, y: 0.5}\n");
        sb.Append("  spritePixelsToUnits: 100\n");
        sb.Append("  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n");
        sb.Append("  spriteGenerateFallbackPhysicsShape: 1\n");
        sb.Append("  alphaUsage: 1\n");
        sb.Append("  alphaIsTransparency: 1\n");
        sb.Append("  spriteTessellationDetail: -1\n");
        sb.Append("  textureType: 8\n");
        sb.Append("  textureShape: 1\n");
        sb.Append("  singleChannelComponent: 0\n");
        sb.Append("  flipbookRows: 1\n");
        sb.Append("  flipbookColumns: 1\n");
        sb.Append("  maxTextureSizeSet: 0\n");
        sb.Append("  compressionQualitySet: 0\n");
        sb.Append("  textureFormatSet: 0\n");
        sb.Append("  ignorePngGamma: 0\n");
        sb.Append("  applyGammaDecoding: 0\n");
        sb.Append("  platformSettings:\n");
        sb.Append("  - serializedVersion: 3\n");
        sb.Append("    buildTarget: DefaultTexturePlatform\n");
        sb.Append("    maxTextureSize: 2048\n");
        sb.Append("    resizeAlgorithm: 0\n");
        // -1 = Automatic format; 0 = Uncompressed, so the palette survives byte-exact.
        sb.Append("    textureFormat: -1\n");
        sb.Append("    textureCompression: 0\n");
        sb.Append("    compressionQuality: 50\n");
        sb.Append("    crunchedCompression: 0\n");
        sb.Append("    allowsAlphaSplitting: 0\n");
        sb.Append("    overridden: 0\n");
        sb.Append("    androidETC2FallbackOverride: 0\n");
        sb.Append("    forceMaximumCompressionQuality_BC6H: 0\n");
        sb.Append("  spriteSheet:\n");
        sb.Append("    serializedVersion: 2\n");

        if (sprites.Count == 0)
        {
            sb.Append("    sprites: []\n");
        }
        else
        {
            sb.Append("    sprites:\n");
            foreach (var s in sprites)
                AppendSprite(sb, s);
        }

        sb.Append("    outline: []\n");
        sb.Append("    physicsShape: []\n");
        sb.Append("    bones: []\n");
        sb.Append("    spriteID: \n");
        sb.Append("    internalID: 0\n");
        sb.Append("    vertices: []\n");
        sb.Append("    indices: \n");
        sb.Append("    edges: []\n");
        sb.Append("    weights: []\n");
        sb.Append("    secondaryTextures: []\n");
        sb.Append(CultureInfo.InvariantCulture, $"    nameFileIdTable:\n");
        foreach (var s in sprites)
            sb.Append(CultureInfo.InvariantCulture, $"      {s.Name}: {s.InternalId}\n");

        sb.Append("  spritePackingTag: \n");
        sb.Append("  pSDRemoveMatte: 0\n");
        sb.Append("  pSDShowRemoveMatteOption: 0\n");
        sb.Append("  userData: \n");
        sb.Append(CultureInfo.InvariantCulture, $"  assetBundleName: \n");
        sb.Append("  assetBundleVariant: \n");

        return sb.ToString();
    }

    private static void AppendSprite(StringBuilder sb, UnitySprite s)
    {
        sb.Append("    - serializedVersion: 2\n");
        sb.Append(CultureInfo.InvariantCulture, $"      name: {s.Name}\n");
        sb.Append("      rect:\n");
        sb.Append("        serializedVersion: 2\n");
        sb.Append(CultureInfo.InvariantCulture, $"        x: {s.X}\n");
        sb.Append(CultureInfo.InvariantCulture, $"        y: {s.Y}\n");
        sb.Append(CultureInfo.InvariantCulture, $"        width: {s.Width}\n");
        sb.Append(CultureInfo.InvariantCulture, $"        height: {s.Height}\n");
        sb.Append(CultureInfo.InvariantCulture, $"      alignment: {s.Alignment}\n");
        sb.Append(CultureInfo.InvariantCulture, $"      pivot: {{x: {Num(s.PivotX)}, y: {Num(s.PivotY)}}}\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"      border: {{x: {s.BorderLeft}, y: {s.BorderBottom}, z: {s.BorderRight}, w: {s.BorderTop}}}\n");
        sb.Append("      outline: []\n");
        sb.Append("      physicsShape: []\n");
        sb.Append("      tessellationDetail: 0\n");
        sb.Append("      bones: []\n");
        sb.Append(CultureInfo.InvariantCulture, $"      spriteID: {s.SpriteId}\n");
        sb.Append(CultureInfo.InvariantCulture, $"      internalID: {s.InternalId}\n");
        sb.Append("      vertices: []\n");
        sb.Append("      indices: \n");
        sb.Append("      edges: []\n");
        sb.Append("      weights: []\n");
    }

    private static List<UnitySprite> BuildSprites(PackedSheet sheet)
    {
        var info = sheet.Info;
        var sheetHeight = sheet.Image.Height;
        // Covering, not plain Resolve: a packed frame with no sprite rect cannot be sliced in Unity at all,
        // and slicing it by hand is overwritten on the next re-export.
        var animations = SheetAnimationGrouping.ResolveCovering(sheet);
        var result = new List<UnitySprite>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        // The doc comment on StableInternalId promises uniqueness ("Unity rejects duplicate ids within one
        // asset") but a 31-bit hash alone cannot deliver it — bump on collision, as the names already do.
        var usedInternalIds = new HashSet<int>();

        foreach (var anim in animations)
        {
            var prefix = Sanitize(anim.NameOr(info.SpriteName));

            for (var i = 0; i < anim.Frames.Count; i++)
            {
                var frame = anim.Frames[i];
                var name = $"{prefix}_{i}";

                // A frame shared by two differently-named tags legitimately produces two sprites over the
                // same rect; an outright name clash would make Unity drop one silently, so disambiguate.
                var unique = name;
                var suffix = 1;
                while (!usedNames.Add(unique))
                    unique = $"{name}#{suffix++}";

                var (pivotX, pivotY, alignment) = ResolvePivot(info, frame);
                var (borderLeft, borderBottom, borderRight, borderTop) = ResolveBorder(info, frame);

                var internalId = StableInternalId(info.ImageFileName + "/" + unique);
                while (!usedInternalIds.Add(internalId))
                    internalId = internalId == int.MaxValue ? 1 : internalId + 1;

                result.Add(new UnitySprite
                {
                    Name = unique,
                    X = frame.Frame.Left,
                    // Unity texture space is bottom-up.
                    Y = sheetHeight - frame.Frame.Top - frame.Frame.Height,
                    Width = frame.Frame.Width,
                    Height = frame.Frame.Height,
                    Alignment = alignment,
                    PivotX = pivotX,
                    PivotY = pivotY,
                    BorderLeft = borderLeft,
                    BorderBottom = borderBottom,
                    BorderRight = borderRight,
                    BorderTop = borderTop,
                    SpriteId = SheetAnimationGrouping.StableHex128("pix2d/sprite/" + info.ImageFileName + "/" + unique),
                    InternalId = internalId
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Re-bases the canvas-space nine-slice margins onto the frame's own rect, in Unity's y-up border order.
    /// <para>
    /// The margins are measured from the canvas edges, but a trimmed frame's rect starts at
    /// <c>SpriteSourceRect</c> — copying them across unchanged misplaces every slice line by exactly the trim
    /// offset. Borders that no longer fit the trimmed rect are dropped rather than clamped: Unity clamps them
    /// silently into a degenerate 9-slice, so emitting nothing is the more honest failure. (Godot's preset
    /// avoids this whole class of problem by carrying the trim as <c>AtlasTexture.margin</c> instead.)
    /// </para>
    /// </summary>
    private static (int Left, int Bottom, int Right, int Top) ResolveBorder(SheetInfo info, PackedFrame frame)
    {
        if (info.NineSlice is not { } ns)
            return (0, 0, 0, 0);

        // Unity's border is y-up: "bottom" is the canvas' bottom margin, and the frame's own bottom margin is
        // whatever trimming left below the content.
        var left = ns.Left - frame.SpriteSourceRect.Left;
        var top = ns.Top - frame.SpriteSourceRect.Top;
        var right = ns.Right - (frame.SourceSize.Width - frame.SpriteSourceRect.Left - frame.Frame.Width);
        var bottom = ns.Bottom - (frame.SourceSize.Height - frame.SpriteSourceRect.Top - frame.Frame.Height);

        left = Math.Max(0, left);
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);

        // A border wider than the rect it slices is meaningless.
        if (left + right >= frame.Frame.Width || top + bottom >= frame.Frame.Height)
            return (0, 0, 0, 0);

        return (left, bottom, right, top);
    }

    /// <summary>
    /// Maps the sprite's canvas-space pivot into the frame's own normalized, y-up space. Returns Unity's
    /// Center alignment (0) with a 0.5/0.5 pivot when the sprite has no pivot set, and Custom (9) otherwise.
    /// </summary>
    private static (float X, float Y, int Alignment) ResolvePivot(SheetInfo info, PackedFrame frame)
    {
        if (info.Pivot is not { } pivot || frame.Frame.Width == 0 || frame.Frame.Height == 0)
            return (0.5f, 0.5f, 0);

        // The pivot is canvas-space; a trimmed frame's content starts at SpriteSourceRect, so the pivot has
        // to be re-based onto the trimmed rect before being normalized.
        var localX = pivot.X - frame.SpriteSourceRect.Left;
        var localY = pivot.Y - frame.SpriteSourceRect.Top;

        var x = localX / (float)frame.Frame.Width;
        var y = 1f - localY / (float)frame.Frame.Height;
        return (x, y, 9);
    }

    /// <summary>
    /// Unity's <c>internalID</c> is a signed 32-bit file id. Derived deterministically and forced positive
    /// and non-zero (0 is reserved for "unset", and Unity rejects duplicate ids within one asset).
    /// </summary>
    private static int StableInternalId(string seed)
    {
        var hex = SheetAnimationGrouping.StableHex128(seed)[..8];
        var value = (int)(uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0x7fffffff);
        return value == 0 ? 1 : value;
    }

    private static string Num(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Sprite names land in unquoted YAML scalars and become Unity sub-asset names, so anything that would
    /// break the line or the mapping (colons, newlines, leading indicators) is replaced.
    /// </summary>
    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');

        var cleaned = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(cleaned) ? "sprite" : cleaned;
    }

    private sealed class UnitySprite
    {
        public required string Name { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int Alignment { get; init; }
        public float PivotX { get; init; }
        public float PivotY { get; init; }
        public int BorderLeft { get; init; }
        public int BorderBottom { get; init; }
        public int BorderRight { get; init; }
        public int BorderTop { get; init; }
        public required string SpriteId { get; init; }
        public int InternalId { get; init; }
    }
}
