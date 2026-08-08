using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Pix2d.CommonNodes;
using Pix2d.Export.Sheet;
using Pix2d.Export.Sheet.Metadata;
using Pix2d.Project;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Extensions;

// Headless Pix2D CLI (roadmap H2.2 PR-2). Batch-exports .pix2d projects to sprite sheets + engine
// metadata for CI pipelines. Reuses the same sheet engine as the in-app exporter (SpriteSheetBuilder).
//
//   pix2d export <project.pix2d> --spritesheet <out.png> [--data <out.json>] [options]
//   pix2d list   <project.pix2d>
//   pix2d --version | --help
//
// Exit codes: 0 ok, 1 export/runtime error, 2 bad arguments / file not found.

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    PrintUsage();
    return args.Length == 0 ? 2 : 0;
}

if (args[0] is "--version" or "-v")
{
    Console.WriteLine(GetVersion());
    return 0;
}

// Node types + adorner layer must be initialised before any .pix2d is deserialized (the app does this
// during startup; headless we only need a non-null viewport provider — nothing renders through it).
ProjectFormat.EnsureInitialized([typeof(Pix2dSprite).Assembly]);
AdornerLayer.Initialize(new HeadlessViewPortProvider());

try
{
    var rest = args.Skip(1).ToArray();
    return args[0] switch
    {
        "export" => await RunExport(rest),
        "list" => await RunList(rest),
        var other => Fail($"unknown command '{other}'. Try 'pix2d --help'.")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine("error: " + (ex.InnerException ?? ex).Message);
    return 1;
}

// ---------------------------------------------------------------------------------------------------

async Task<int> RunExport(string[] a)
{
    var flags = Flags.Parse(a);
    var input = flags.Positional.FirstOrDefault();
    if (input is null) return Fail("export: missing <project.pix2d>.");
    if (!File.Exists(input)) return Fail($"export: file not found: {input}");

    var sheetPath = flags.Get("spritesheet");
    if (string.IsNullOrWhiteSpace(sheetPath)) return Fail("export: --spritesheet <out.png> is required.");

    var scene = await LoadScene(input);
    var sprite = SelectArtboard(scene, flags.Get("artboard"), out var artboardError);
    if (sprite is null) return Fail("export: " + artboardError);

    var packMode = (flags.Get("sheet-type") ?? "grid").Trim().ToLowerInvariant() switch
    {
        "tight" => SheetPackMode.Tight,
        "grid" => SheetPackMode.Grid,
        var bad => throw new ArgumentException($"--sheet-type must be 'grid' or 'tight', got '{bad}'.")
    };

    // Resolve --tag here rather than letting the builder throw: a mistyped tag name is a usage error
    // (exit 2) and deserves the list of what the sprite actually has.
    var tag = flags.Get("tag");
    if (!string.IsNullOrWhiteSpace(tag))
    {
        var match = sprite.AnimationTags?
            .FirstOrDefault(t => string.Equals(t.Name, tag, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            var available = sprite.AnimationTags is { Count: > 0 } tags
                ? string.Join(", ", tags.Select(t => $"'{t.Name}'"))
                : "(this artboard has no animation tags)";
            return Fail($"export: --tag '{tag}' not found in artboard '{sprite.Name}'. Available: {available}.");
        }

        tag = match.Name; // adopt the tag's own casing for the message below
    }

    var options = new SpriteSheetOptions
    {
        PackMode = packMode,
        MaxColumns = flags.GetInt("columns") ?? 4,
        Padding = flags.GetInt("padding") ?? 0,
        Trim = flags.Has("trim"),
        PowerOfTwo = flags.Has("pot"),
        TagFilter = string.IsNullOrWhiteSpace(tag) ? null : tag,
        SpriteName = Path.GetFileNameWithoutExtension(sheetPath),
        ImageFileName = Path.GetFileName(sheetPath)
    };
    var scale = flags.GetDouble("scale") ?? 1;

    using var sheet = SpriteSheetBuilder.Build(sprite, scale, options);

    EnsureDirectory(sheetPath);
    await using (var png = sheet.Image.ToPngStream())
    await using (var fs = File.Create(sheetPath))
        await png.CopyToAsync(fs);
    Console.WriteLine($"wrote {sheetPath}  ({sheet.Image.Width}x{sheet.Image.Height}, {sheet.Frames.Count} frame(s), {options.PackMode.ToString().ToLowerInvariant()} pack{(options.Trim ? ", trimmed" : "")}{(options.TagFilter is null ? "" : $", tag '{options.TagFilter}'")})");

    var dataPath = flags.Get("data");
    if (!string.IsNullOrWhiteSpace(dataPath))
    {
        var formatId = flags.Get("format") ?? "aseprite";
        var emitter = SheetMetadataEmitters.TryGet(formatId);
        if (emitter is null)
            return Fail($"export: unknown --format '{formatId}'. Available: {string.Join(", ", SheetMetadataEmitters.All.Select(e => e.Id))}.");

        var json = emitter.Emit(sheet, new SheetMetadataOptions { AppVersion = GetVersion() });
        EnsureDirectory(dataPath);
        await File.WriteAllTextAsync(dataPath, json);
        Console.WriteLine($"wrote {dataPath}  ({emitter.DisplayName})");
    }

    return 0;
}

async Task<int> RunList(string[] a)
{
    var flags = Flags.Parse(a);
    var input = flags.Positional.FirstOrDefault();
    if (input is null) return Fail("list: missing <project.pix2d>.");
    if (!File.Exists(input)) return Fail($"list: file not found: {input}");

    var scene = await LoadScene(input);
    var sprites = scene.Nodes.OfType<Pix2dSprite>().ToList();

    var doc = new
    {
        file = Path.GetFileName(input),
        artboards = sprites.Select((s, i) => new
        {
            index = i,
            name = s.Name,
            width = (int)s.Size.Width,
            height = (int)s.Size.Height,
            layers = s.Layers.Count(),
            frames = s.GetFramesCount(),
            fps = s.FrameRate,
            // Tag names are what `export --tag` takes, so they have to be discoverable without opening
            // the project in the app — this is the lookup step of an agent/CI pipeline.
            defaultFrameDurationMs = s.DefaultFrameDurationMs,
            tags = (s.AnimationTags ?? [])
                .Select(t => new { name = t.Name, from = t.From, to = t.To, direction = t.GetDirectionKey() })
                .ToArray()
        }).ToArray()
    };

    Console.WriteLine(JsonConvert.SerializeObject(doc, Formatting.Indented));
    return 0;
}

// ---------------------------------------------------------------------------------------------------

async Task<SKNode> LoadScene(string path)
{
    // The load path prints diagnostics (e.g. "[ProjectFormat] Migrated scene v1 -> v2.") via Console;
    // route them to stderr so `pix2d list` keeps a clean, pipeable JSON payload on stdout.
    var savedOut = Console.Out;
    Console.SetOut(Console.Error);
    try
    {
        await using var fs = File.OpenRead(path);
        return await ProjectUnpacker.LoadProjectSceneFromStream(fs, path)
               ?? throw new Exception($"could not load a scene from {path} (not a valid .pix2d project?).");
    }
    finally
    {
        Console.SetOut(savedOut);
    }
}

static Pix2dSprite? SelectArtboard(SKNode scene, string? selector, out string error)
{
    error = "";
    var sprites = scene.Nodes.OfType<Pix2dSprite>().ToList();
    if (sprites.Count == 0)
    {
        error = "the project has no artboards.";
        return null;
    }

    if (string.IsNullOrWhiteSpace(selector))
        return sprites[0];

    if (int.TryParse(selector, out var idx))
    {
        if (idx >= 0 && idx < sprites.Count) return sprites[idx];
        error = $"--artboard index {idx} is out of range (0..{sprites.Count - 1}).";
        return null;
    }

    var byName = sprites.FirstOrDefault(s => string.Equals(s.Name, selector, StringComparison.OrdinalIgnoreCase));
    if (byName != null) return byName;

    error = $"--artboard '{selector}' not found. Available: {string.Join(", ", sprites.Select(s => s.Name))}.";
    return null;
}

static void EnsureDirectory(string filePath)
{
    var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
}

static int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    return 2;
}

static string GetVersion() =>
    Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

static void PrintUsage()
{
    Console.WriteLine(
"""
pix2d — headless sprite/animation exporter

Usage:
  pix2d export <project.pix2d> --spritesheet <out.png> [--data <out.json>] [options]
  pix2d list   <project.pix2d>
  pix2d --version | --help

export options:
  --spritesheet <path>   Output sheet PNG (required).
  --data <path>          Also write a metadata sidecar to this path.
  --format <id>          Metadata format for --data (default: aseprite):
                           aseprite  Aseprite --data JSON (most portable)
                           godot     Godot 4 SpriteFrames .tres
                           unity     Unity texture .png.meta (pre-sliced sprites)
                           libgdx    libGDX TexturePacker .atlas
                         Name --data with the format's own extension (.tres/.png.meta/.atlas);
                         for unity it must be <sheet>.png.meta next to the sheet PNG.
  --sheet-type <mode>    Packing: grid | tight (default: grid).
  --columns <n>          Columns in grid mode (default: 4).
  --padding <n>          Transparent gutter between frames, in px (default: 0).
  --trim                 Crop frames to their opaque bounds.
  --pot                  Round the sheet size up to a power of two.
  --scale <n>            Render scale, 1..N (default: 1).
  --artboard <name|idx>  Which artboard to export (default: the first).
  --tag <name>           Export only this animation tag's frame range. The sheet is re-based to
                         frame 0 (as Aseprite's own --tag does). `list` prints the tag names.

Examples:
  pix2d export hero.pix2d --spritesheet hero.png --data hero.json
  pix2d export hero.pix2d --spritesheet hero.png --sheet-type tight --trim --pot
  pix2d export hero.pix2d --spritesheet run.png --data run.json --tag run
  pix2d list hero.pix2d
""");
}

// ---------------------------------------------------------------------------------------------------

/// <summary>Tiny flag parser: <c>--key value</c> pairs (value optional for known switches) + positionals.</summary>
sealed class Flags
{
    private static readonly HashSet<string> Switches =
        new(StringComparer.OrdinalIgnoreCase) { "trim", "pot" };

    private readonly Dictionary<string, string?> _map = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Positional { get; } = [];

    public static Flags Parse(string[] args)
    {
        var f = new Flags();
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var key = token[2..];
                if (Switches.Contains(key))
                    f._map[key] = null; // boolean switch, consumes no value
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    f._map[key] = args[++i];
                else
                    f._map[key] = null;
            }
            else
            {
                f.Positional.Add(token);
            }
        }

        return f;
    }

    public bool Has(string key) => _map.ContainsKey(key);
    public string? Get(string key) => _map.TryGetValue(key, out var v) ? v : null;
    public int? GetInt(string key) => _map.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : null;

    public double? GetDouble(string key) =>
        _map.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
}

/// <summary>Headless stub — supplies a non-null provider with no live viewport (nothing renders through it).</summary>
sealed class HeadlessViewPortProvider : IViewPortProvider
{
    public ViewPort ViewPort => null!;
}
