using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Pix2d.CommonNodes;
using Pix2d.Project;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Serialization;

// Format hardening test harness for the .pix2d format (roadmap H1.2). Two jobs:
//   1. Backward-compat corpus: load every corpus file through the exact production path
//      (ProjectUnpacker → ProjectFormat.DeserializeScene → NodeSerializer), assert structure, and
//      round-trip (re-serialize with stable keys → reload → counts preserved).
//   2. Serialization contract: pin the exact set of serialized properties per persisted type in a
//      checked-in snapshot, so a settable property silently entering/leaving the format is caught.
//
// Pass --update-contract to (re)write the contract snapshot after a deliberate schema change.

var updateContract = args.Contains("--update-contract");
var positional = args.Where(a => !a.StartsWith("--")).ToArray();

// Mirror the desktop bootstrapper's registration. All persisted node types live in Pix2d.Shared;
// if a persisted type is ever defined in Pix2d.Core, add that assembly here (and reference it).
ProjectFormat.EnsureInitialized([typeof(Pix2dSprite).Assembly]);

// Node constructors (DrawingContainerBaseNode) touch the adorner layer, which the app normally wires
// during startup. Headless we only need the provider to be non-null; a null ViewPort is fine for a
// pure load/round-trip check (nothing renders through a live viewport here).
AdornerLayer.Initialize(new HeadlessViewPortProvider());

var warnings = new List<string>();
NodeTypeRegistry.OnWarning = msg => warnings.Add(msg);

var corpusDir = positional.Length > 0 ? positional[0] : LocateCorpus();
if (corpusDir is null || !Directory.Exists(corpusDir))
{
    Console.Error.WriteLine($"Corpus folder not found (looked for TestImages/; arg='{(positional.Length > 0 ? positional[0] : "<none>")}').");
    return 2;
}

var repoRoot = Directory.GetParent(corpusDir)!.FullName;
var contractPath = Path.Combine(repoRoot, "Sources", "Tools", "Pix2d.FormatTests", "format-contract.json");

if (updateContract)
{
    WriteContract(contractPath, ComputeContract());
    Console.WriteLine($"Serialization contract snapshot written to {contractPath}");
    return 0;
}

var contractOk = CheckContract(contractPath);

var files = Directory.GetFiles(corpusDir, "*.pix2d").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
if (files.Length == 0)
{
    Console.Error.WriteLine($"No .pix2d files in {corpusDir}");
    return 2;
}

Console.WriteLine($"Format corpus test — {files.Length} file(s) in {corpusDir}\n");

int passed = 0, failed = 0, corrupt = 0;
foreach (var file in files)
{
    warnings.Clear();
    var name = Path.GetFileName(file);

    // A structurally broken archive (unreadable zip / no project.json) is not a format-compatibility
    // concern — flag it separately so it never masquerades as a regression.
    if (IsCorruptArchive(file, out var corruptReason))
    {
        corrupt++;
        Console.WriteLine($"  CORRUPT {name,-32} {corruptReason}");
        continue;
    }

    try
    {
        await using var fs = File.OpenRead(file);
        var scene = await ProjectUnpacker.LoadProjectSceneFromStream(fs, file);

        var problems = Validate(scene, out var stats);
        problems.AddRange(warnings.Where(w => w.Contains("Skipping unknown node")));

        // Exercise the write path too: re-serialize (must emit stable $type keys) and reload at the
        // current version, then confirm the structure survived the round-trip unchanged.
        if (scene is not null && problems.Count == 0)
            problems.AddRange(RoundTrip(scene));

        if (problems.Count == 0)
        {
            passed++;
            Console.WriteLine($"  PASS  {name,-34} {stats}");
            foreach (var w in warnings)
                Console.WriteLine($"        · {w}");
        }
        else
        {
            failed++;
            Console.WriteLine($"  FAIL  {name,-34} {stats}");
            foreach (var p in problems)
                Console.WriteLine($"        ! {p}");
        }
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"  FAIL  {name,-34} threw {ex.GetType().Name}: {(ex.InnerException ?? ex).Message}");
    }
}

Console.WriteLine($"\n{passed}/{files.Length - corrupt} loadable files passed, {failed} failed" +
                  (corrupt > 0 ? $", {corrupt} corrupt (ignored)." : "."));
Console.WriteLine($"Serialization contract: {(contractOk ? "OK" : "DRIFT DETECTED")}.");
return failed == 0 && contractOk ? 0 : 1;

// --- helpers ---

// The exact set of properties each persisted type serializes, computed from the real resolver so
// there is no logic to keep in sync. This is the explicit, enforceable form of the format's property
// contract (replacing the implicit "any property with a setter" heuristic as the source of truth).
static SortedDictionary<string, string[]> ComputeContract()
{
    var resolver = new WriteOnlyPropertiesContractResolver();
    var contract = new SortedDictionary<string, string[]>(StringComparer.Ordinal);

    // Registered node types, keyed by their stable $type key.
    foreach (var (key, type) in NodeTypeRegistry.Registrations)
        AddContractEntry(contract, resolver, key, type);

    // Nested value objects that are serialized inline on nodes. They aren't registered node types, so
    // without this they'd have zero drift protection — a change to their persisted fields (e.g.
    // pruning NodeDesignerState.IsSelected) would go unnoticed. Keyed with a '~' prefix to set them
    // apart from node keys.
    AddContractEntry(contract, resolver, "~DesignerState", typeof(NodeDesignerState));
    AddContractEntry(contract, resolver, "~ExportSettings", typeof(NodeExportSettings));
    AddContractEntry(contract, resolver, "~OnionSkinSettings", typeof(OnionSkinSettings));
    AddContractEntry(contract, resolver, "~AnimationTag", typeof(SpriteAnimationTag));
    AddContractEntry(contract, resolver, "~NineSliceMargins", typeof(NineSliceMargins));

    return contract;
}

static void AddContractEntry(SortedDictionary<string, string[]> contract, WriteOnlyPropertiesContractResolver resolver, string key, Type type)
{
    if (resolver.ResolveContract(type) is not JsonObjectContract objectContract)
        return;

    contract[key] = objectContract.Properties
        .Where(p => !p.Ignored && (p.ShouldSerialize is null || p.ShouldSerialize(null!)))
        .Select(p => p.PropertyName!)
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();
}

static void WriteContract(string path, SortedDictionary<string, string[]> contract) =>
    File.WriteAllText(path, JsonConvert.SerializeObject(contract, Formatting.Indented));

static bool CheckContract(string path)
{
    var current = ComputeContract();

    if (!File.Exists(path))
    {
        Console.WriteLine($"  (no contract snapshot yet at {Path.GetFileName(path)} — run with --update-contract to create it)\n");
        return false;
    }

    var baseline = JsonConvert.DeserializeObject<SortedDictionary<string, string[]>>(File.ReadAllText(path))
                   ?? new SortedDictionary<string, string[]>(StringComparer.Ordinal);

    var drift = new List<string>();
    foreach (var key in current.Keys.Union(baseline.Keys).OrderBy(k => k, StringComparer.Ordinal))
    {
        var now = current.TryGetValue(key, out var n) ? n : [];
        var was = baseline.TryGetValue(key, out var b) ? b : [];
        foreach (var added in now.Except(was))
            drift.Add($"    + {key}.{added}  (now serialized — not in contract)");
        foreach (var removed in was.Except(now))
            drift.Add($"    - {key}.{removed}  (was serialized — no longer emitted)");
    }

    if (drift.Count == 0)
        return true;

    Console.WriteLine("  Serialization contract DRIFT — the set of persisted properties changed:");
    foreach (var d in drift)
        Console.WriteLine(d);
    Console.WriteLine("  If intentional: add a migration if it breaks old files, then run --update-contract.\n");
    return false;
}

static (int sprites, int layers, int frames, int bitmaps) Counts(SKNode scene)
{
    var sprites = scene.Nodes.OfType<Pix2dSprite>().ToList();
    var layers = sprites.SelectMany(s => s.Layers).ToList();
    var frames = layers.SelectMany(l => l.Nodes.OfType<SpriteNode>()).ToList();
    var bitmaps = frames.Count(n => n.Bitmap is { Width: > 0, Height: > 0 });
    return (sprites.Count, layers.Count, frames.Count, bitmaps);
}

static List<string> Validate(SKNode? scene, out string stats)
{
    var problems = new List<string>();
    if (scene is null)
    {
        stats = "scene = null";
        problems.Add("deserialized scene is null");
        return problems;
    }

    var (sprites, layers, frames, bitmaps) = Counts(scene);
    stats = $"sprites={sprites} layers={layers} frames={frames} bitmaps={bitmaps}";

    if (sprites == 0)
        problems.Add("no Pix2dSprite artboards found");
    foreach (var s in scene.Nodes.OfType<Pix2dSprite>().Where(s => !s.Layers.Any()))
        problems.Add($"sprite '{s.Name}' has no layers");
    if (frames > 0 && bitmaps == 0)
        problems.Add("no frame bitmaps linked (image entries failed to bind)");

    // A sprite whose first layer holds frame nodes must report a non-zero GetFramesCount(). Legacy
    // files store frames as raw child nodes with an empty Frames metadata list, rebuilt lazily only on
    // frame *access*; if counting doesn't trigger that init, headless exporters (CLI / SpriteSheetBuilder)
    // see 0 frames and emit empty sheets. (The count can legitimately EXCEED the node count — modern
    // files persist empty/linked frames that carry no distinct bitmap node — so this only guards the
    // zero case, which is the real regression.)
    foreach (var s in scene.Nodes.OfType<Pix2dSprite>())
    {
        var firstLayerFrameNodes = s.Layers.FirstOrDefault()?.Nodes.OfType<SpriteNode>().Count() ?? 0;
        if (firstLayerFrameNodes > 0 && s.GetFramesCount() == 0)
            problems.Add($"sprite '{s.Name}': GetFramesCount()==0 but first layer has " +
                         $"{firstLayerFrameNodes} frame node(s) — legacy frame metadata not initialized on count");
    }

    return problems;
}

// Re-serialize with the current writer, assert stable $type keys are used, then reload and confirm
// the structure is preserved. Guards the write path (BindToName stable keys) and full round-trip.
static List<string> RoundTrip(SKNode scene)
{
    var problems = new List<string>();
    var before = Counts(scene);

    using var serializer = new NodeSerializer();
    var json = serializer.Serialize(scene);

    if (before.sprites > 0 && !json.Contains("\"$type\": \"Sprite\""))
        problems.Add("re-serialized JSON does not use the stable 'Sprite' $type key");
    if (json.Contains("\"$type\": \"Pix2d."))
        problems.Add("re-serialized JSON still emits a full-name $type (stable key missing)");

    var reloaded = ProjectFormat.DeserializeScene(json, ProjectFormat.CurrentVersion, serializer.GetDataEntries());
    var after = Counts(reloaded);

    if (before != after)
        problems.Add($"round-trip changed structure: {before} -> {after}");

    return problems;
}

static bool IsCorruptArchive(string file, out string reason)
{
    try
    {
        using var zip = ZipFile.OpenRead(file);
        if (zip.Entries.Count == 0)
        {
            reason = "empty archive (0 entries)";
            return true;
        }
        if (zip.GetEntry("project.json") is null && zip.GetEntry("pix2d.json") is null)
        {
            reason = "no project.json entry";
            return true;
        }
    }
    catch (Exception ex)
    {
        reason = $"unreadable zip: {ex.Message}";
        return true;
    }

    reason = "";
    return false;
}

static string? LocateCorpus()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "TestImages");
        if (Directory.Exists(candidate))
            return candidate;
    }
    return null;
}

/// <summary>Headless stub — supplies a non-null provider with no live viewport.</summary>
sealed class HeadlessViewPortProvider : IViewPortProvider
{
    public ViewPort ViewPort => null!;
}
