using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Themes.Simple;
using Microsoft.Extensions.DependencyInjection;
using Mvvm.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Import.Flow;
using Pix2d.Plugins.ImageFormats.PiskelFormat;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.Project;
using Pix2d.UI;
using Pix2d.UI.Layers;
using Pix2d.Export;
using Pix2d.Export.Sheet;
using Pix2d.Plugins.PngFormat.Exporters;
using Pix2d.Export.Sheet.Metadata;
using Pix2d.Primitives;
using Pix2d.Primitives.Crash;
using Pix2d.Primitives.Drawing;
using Pix2d.Plugins.Drawing.Brushes;
using Pix2d.Primitives.ViewPort;
using Pix2d.Services;
using Pix2d.ScenarioTests;
using Pix2d.State;
using Pix2d;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaNodes.Render;
using SkiaNodes.Serialization;
using SkiaSharp;

// Headless scenario / integration harness. Boots the real Pix2d DI graph without a window and drives
// it programmatically. Run manually before a release:
//   dotnet run --project Sources/Tools/Pix2d.ScenarioTests
// Exit code 0 = all checks passed, 1 = at least one failed, 2 = boot failed.

static class Runner
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Bring up Avalonia WITHOUT a window: registers the AssetLoader (LocalizationService reads
        // avares://.../strings.json during Initialize), the Dispatcher and the FontManager. No message
        // loop runs — every scenario below is synchronous on this thread.
        try
        {
            // A trivial Application (not EditorApp) — we only need the platform's AssetLoader,
            // Dispatcher and FontManager, not EditorApp's window/bootstrapper coupling (its
            // OnFrameworkInitializationCompleted runs during Setup and needs a HostView + bootstrapper).
            AppBuilder.Configure<HarnessApp>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: Avalonia headless setup failed: " + ex);
            return 2;
        }

        HeadlessHarness harness;
        try
        {
            harness = HeadlessHarness.Boot(size: 64);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: harness boot failed: " + ex);
            return 2;
        }

        var t = new TestReport();

        CommandInventory(harness, t);
        ActivateEveryTool(harness, t);
        DrawAndUndoRedo(harness, t);
        LayerScenario(harness, t);
        FrameScenario(harness, t);
        ExportScenario(harness, t);
        SpriteSheetExportScenario(harness, t);
        EnginePresetExportScenario(harness, t);
        PiskelImportScenario(harness, t);
        LinkedCelsScenario(harness, t);
        ArtboardScenario(harness, t);
        DegenerateCanvasScenario(harness, t);
        OversizedCanvasScenario(harness, t);
        MoveThumbSelectionChurnScenario(harness, t);
        ReadOnlyOverwriteScenario(harness, t);
        LoggerTargetFailureScenario(harness, t);
        AnimationMetaScenario(harness, t);
        BrushPresetScenario(harness, t);
        BatchExportScenario(harness, t);
        PrecisionScrollDetectorScenario(harness, t);
        PixelSelectionScenario(harness, t);
        SelectionCombineScenario(harness, t);
        GeneralContextObjectToolScenario(harness, t);
        GeneralContextObjectCommandsScenario(harness, t);
        EyedropperReturnScenario(harness, t);
        GridAppearanceScenario(harness, t);
        FillOpacityScenario(harness, t);
        ShapeBrushScenario(harness, t);
        SymmetryScenario(harness, t);
        LayerTitleAndPixelMaskScenario(harness, t);
        UpdateReleaseParsingScenario(t);
        NativeCrashSignatureScenario(t);
        SafeSweep(harness);

        return t.Summarize();
    }

    // --- Scenario 1: enumerate every registered command (coverage inventory) ------------------------
    static void CommandInventory(HeadlessHarness h, TestReport t)
    {
        var commands = h.Commands.GetCommands().OrderBy(c => c.Name).ToArray();
        Console.WriteLine($"\n=== Command inventory: {commands.Length} commands registered ===");
        foreach (var c in commands)
        {
            var ctx = c.EditContextType?.ToString() ?? "-";
            var sc = c.GetShortcutString();
            Console.WriteLine($"  {c.Name,-40} ctx={ctx,-10} {(string.IsNullOrEmpty(sc) ? "" : "[" + sc + "]")}");
        }

        t.Check("inventory: at least 40 commands are registered", () => Assert.True(commands.Length >= 40,
            $"expected >= 40 commands, got {commands.Length}"));
        t.Check("inventory: no command has a null/empty name", () =>
            Assert.True(commands.All(c => !string.IsNullOrWhiteSpace(c.Name)), "found a command with an empty name"));
    }

    // --- Scenario 2: execute the tool-activation commands (all synchronous, all undo-neutral) --------
    static void ActivateEveryTool(HeadlessHarness h, TestReport t)
    {
        var toolCommands = h.Commands.GetCommands()
            .Where(c => c.Name.StartsWith("Tools.", StringComparison.Ordinal))
            .OrderBy(c => c.Name)
            .ToArray();

        Console.WriteLine($"\n=== Executing {toolCommands.Length} tool-activation commands ===");
        foreach (var cmd in toolCommands)
        {
            t.Check($"execute {cmd.Name}", () =>
            {
                var task = h.Commands.ExecuteCommandAsync(cmd.Name);
                // Tool activations are synchronous; the returned task is already complete. Guard anyway.
                if (!task.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("command did not complete synchronously");
            });
        }
    }

    // --- Scenario 3: draw a pixel, assert it landed, assert undo/redo round-trips -------------------
    static void DrawAndUndoRedo(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Draw + undo/redo scenario ===");
        const int px = 10, py = 10;

        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Red);

        var undoBefore = h.Operations.UndoOperationsCount;

        h.DrawPixel(px, py);

        var drawn = h.NonEmptyPixels().ToArray();
        Console.WriteLine($"  [diag] active sprite non-empty pixels: {drawn.Length}");
        foreach (var p in drawn.Take(8))
            Console.WriteLine($"  [diag]   ({p.X},{p.Y}) = {p.Color}");

        t.Check("drawn pixel is red", () =>
        {
            var c = h.GetPixel(px, py);
            Assert.True(c.Alpha > 0 && c.Red == 255 && c.Green == 0 && c.Blue == 0,
                $"expected opaque red at ({px},{py}), got {c}");
        });

        t.Check("drawing pushed exactly one undo operation", () =>
            Assert.True(h.Operations.UndoOperationsCount == undoBefore + 1,
                $"undo count {undoBefore} -> {h.Operations.UndoOperationsCount}, expected +1"));

        t.Check("undo clears the pixel", () =>
        {
            Assert.True(h.Operations.CanUndo, "CanUndo was false before undo");
            h.Operations.Undo();
            var c = h.GetPixel(px, py);
            Assert.True(c.Alpha == 0, $"expected transparent at ({px},{py}) after undo, got {c}");
        });

        t.Check("redo restores the pixel", () =>
        {
            Assert.True(h.Operations.CanRedo, "CanRedo was false after undo");
            h.Operations.Redo();
            var c = h.GetPixel(px, py);
            Assert.True(c.Alpha > 0 && c.Red == 255, $"expected red at ({px},{py}) after redo, got {c}");
        });
    }

    // --- Scenario 4: layers -------------------------------------------------------------------------
    static void LayerScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Layer scenario ===");
        var start = h.LayerCount;

        t.Check("AddLayer adds one layer", () =>
        {
            h.Exec("Sprite.Edit.AddLayer");
            Assert.True(h.LayerCount == start + 1, $"layers {start} -> {h.LayerCount}, expected +1");
        });
        t.Check("DuplicateLayer adds one layer", () =>
        {
            h.Exec("Sprite.Edit.DuplicateLayer");
            Assert.True(h.LayerCount == start + 2, $"layers -> {h.LayerCount}, expected {start + 2}");
        });
        t.Check("DeleteLayer removes one layer", () =>
        {
            h.Exec("Sprite.Edit.DeleteLayer");
            Assert.True(h.LayerCount == start + 1, $"layers -> {h.LayerCount}, expected {start + 1}");
        });
    }

    // --- Scenario 5: animation frames ---------------------------------------------------------------
    static void FrameScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Frame scenario ===");
        var start = h.FrameCount;

        t.Check("AddFrame adds one frame", () =>
        {
            h.Exec("Sprite.Animation.AddFrame");
            Assert.True(h.FrameCount == start + 1, $"frames {start} -> {h.FrameCount}, expected +1");
        });
        t.Check("DuplicateFrame adds one frame", () =>
        {
            h.Exec("Sprite.Animation.DuplicateFrame");
            Assert.True(h.FrameCount == start + 2, $"frames -> {h.FrameCount}, expected {start + 2}");
        });
        t.Check("Next/Prev frame navigation does not throw", () =>
        {
            h.Exec("Sprite.Animation.NextFrame");
            h.Exec("Sprite.Animation.PrevFrame");
        });
        t.Check("DeleteFrame removes one frame", () =>
        {
            h.Exec("Sprite.Animation.DeleteFrame");
            Assert.True(h.FrameCount == start + 1, $"frames -> {h.FrameCount}, expected {start + 1}");
        });
    }

    // --- Scenario 6: PNG export (real render pipeline) ---------------------------------------------
    static void ExportScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== PNG export scenario ===");
        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Lime);
        h.DrawPixel(5, 5);

        var expectedW = (int)h.ActiveSprite.Size.Width;
        var expectedH = (int)h.ActiveSprite.Size.Height;
        var png = h.ExportActivePng();
        Console.WriteLine($"  [diag] exported PNG: {png.Length} bytes, expecting {expectedW}x{expectedH}");

        t.Check("export produces a valid PNG stream", () =>
        {
            byte[] sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            Assert.True(png.Length > sig.Length && png.Take(sig.Length).SequenceEqual(sig),
                $"expected PNG signature, got {png.Length} bytes");
        });

        t.Check("exported PNG decodes to the sprite size with the drawn pixel", () =>
        {
            using var bmp = SKBitmap.Decode(png);
            Assert.True(bmp != null, "SKBitmap.Decode returned null");
            Assert.True(bmp!.Width == expectedW && bmp.Height == expectedH,
                $"decoded {bmp.Width}x{bmp.Height}, expected {expectedW}x{expectedH}");
            var c = bmp.GetPixel(5, 5);
            Assert.True(c.Green == 255 && c.Red == 0 && c.Alpha > 0, $"expected lime at (5,5) in PNG, got {c}");
        });
    }

    // --- Scenario 6b: sprite-sheet v2 build + Aseprite JSON metadata (pure core, no file dialogs) ---
    static void SpriteSheetExportScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Sprite sheet export v2 scenario ===");

        // Fresh 3-frame sprite: draw a distinct pixel on each frame so no frame is fully empty.
        h.NewProject(64);
        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Red);
        h.DrawPixel(5, 5);
        h.Exec("Sprite.Animation.AddFrame");
        h.DrawPixel(10, 10);
        h.Exec("Sprite.Animation.AddFrame");
        h.DrawPixel(20, 20);

        var sprite = h.ActiveSprite;
        var frameCount = sprite.GetFramesCount();
        var canvasW = (int)sprite.Size.Width;
        var canvasH = (int)sprite.Size.Height;
        var expectedDuration = (int)Math.Round(1000f / Math.Max(1f, sprite.FrameRate));

        // --- Grid pack, no trim -------------------------------------------------------------------
        using var grid = SpriteSheetBuilder.Build(sprite, 1, new SpriteSheetOptions
        {
            PackMode = SheetPackMode.Grid,
            MaxColumns = 4,
            SpriteName = "test",
            ImageFileName = "test.png"
        });

        Console.WriteLine($"  [diag] frames={grid.Frames.Count} sheet={grid.Image.Width}x{grid.Image.Height} " +
                          $"(canvas {canvasW}x{canvasH}, {frameCount} frames)");

        t.Check("grid sheet packs every frame", () =>
            Assert.True(grid.Frames.Count == frameCount, $"packed {grid.Frames.Count}, expected {frameCount}"));

        t.Check("grid sheet dimensions match columns x rows", () =>
        {
            var cols = Math.Min(4, frameCount);
            var rows = (int)Math.Ceiling(frameCount / (double)cols);
            Assert.True(grid.Image.Width == cols * canvasW && grid.Image.Height == rows * canvasH,
                $"sheet {grid.Image.Width}x{grid.Image.Height}, expected {cols * canvasW}x{rows * canvasH}");
        });

        t.Check("grid sheet frames are untrimmed, full canvas source size", () =>
            Assert.True(grid.Frames.All(f => !f.Trimmed
                                             && f.SourceSize.Width == canvasW && f.SourceSize.Height == canvasH
                                             && f.Frame.Width == canvasW && f.Frame.Height == canvasH),
                "expected every frame untrimmed and full-canvas"));

        // --- Aseprite JSON emit + parse -----------------------------------------------------------
        var json = new AsepriteJsonEmitter().Emit(grid, new SheetMetadataOptions { AppVersion = "9.9.9" });
        Console.WriteLine("  [diag] json head: " + json.Replace("\r", "").Replace("\n", " ").Substring(0, Math.Min(140, json.Length)));

        t.Check("Aseprite JSON parses and has one frame entry per frame", () =>
        {
            var doc = JObject.Parse(json);
            var frames = (JObject)doc["frames"]!;
            Assert.True(frames.Count == frameCount, $"json frames {frames.Count}, expected {frameCount}");
        });

        t.Check("Aseprite JSON meta matches the sheet (image, size, version)", () =>
        {
            var meta = JObject.Parse(json)["meta"]!;
            Assert.True((string?)meta["image"] == "test.png", $"meta.image = {meta["image"]}");
            Assert.True((int)meta["size"]!["w"]! == grid.Image.Width && (int)meta["size"]!["h"]! == grid.Image.Height,
                "meta.size mismatch");
            Assert.True((string?)meta["version"] == "9.9.9", $"meta.version = {meta["version"]}");
            Assert.True((string?)meta["scale"] == "1", $"meta.scale should be the string \"1\", got {meta["scale"]}");
        });

        t.Check("Aseprite JSON frames carry duration + source geometry", () =>
        {
            var frames = (JObject)JObject.Parse(json)["frames"]!;
            foreach (var (_, val) in frames)
            {
                var f = (JObject)val!;
                Assert.True((int)f["duration"]! == expectedDuration,
                    $"duration {f["duration"]}, expected {expectedDuration} (from {sprite.FrameRate} fps)");
                Assert.True((int)f["sourceSize"]!["w"]! == canvasW && (int)f["sourceSize"]!["h"]! == canvasH,
                    "sourceSize mismatch");
                Assert.True(f["frame"] != null && f["spriteSourceSize"] != null, "missing frame/spriteSourceSize");
            }
        });

        // --- Tight pack + trim: trimmed frames must be smaller than the full canvas ---------------
        using var tight = SpriteSheetBuilder.Build(sprite, 1, new SpriteSheetOptions
        {
            PackMode = SheetPackMode.Tight,
            Trim = true,
            SpriteName = "test",
            ImageFileName = "test.png"
        });

        t.Check("trim shrinks frames below the full canvas", () =>
            Assert.True(tight.Frames.All(f => f.Trimmed && f.SpriteSourceRect.Width < canvasW),
                "expected every single-pixel frame to trim below the canvas width"));

        t.Check("tight+trim sheet is smaller than the untrimmed grid", () =>
            Assert.True(tight.Image.Width * tight.Image.Height < grid.Image.Width * grid.Image.Height,
                $"tight {tight.Image.Width}x{tight.Image.Height} not smaller than grid {grid.Image.Width}x{grid.Image.Height}"));

        // --- PR-3: the animation metadata model reaches the emitted JSON --------------------------
        sprite.AnimationTags =
        [
            new SpriteAnimationTag { Name = "intro", From = 0, To = 0 },
            new SpriteAnimationTag { Name = "loop", From = 1, To = 2, Direction = SpriteAnimationDirection.PingPong }
        ];
        sprite.SetFrameDurationMs(1, 400);
        sprite.ExportPivot = new SKPoint(32, 60);
        sprite.NineSlice = new NineSliceMargins { Left = 8, Top = 6, Right = 8, Bottom = 6 };

        using var tagged = SpriteSheetBuilder.Build(sprite, 1, new SpriteSheetOptions
        {
            PackMode = SheetPackMode.Grid,
            MaxColumns = 4,
            SpriteName = "test",
            ImageFileName = "test.png"
        });
        var taggedJson = new AsepriteJsonEmitter().Emit(tagged, new SheetMetadataOptions { AppVersion = "9.9.9" });

        t.Check("meta.frameTags carries every tag with its range and direction", () =>
        {
            var tags = (JArray)JObject.Parse(taggedJson)["meta"]!["frameTags"]!;
            Assert.True(tags.Count == 2, $"expected 2 frameTags, got {tags.Count}");
            Assert.True((string?)tags[1]["name"] == "loop" && (int)tags[1]["from"]! == 1 && (int)tags[1]["to"]! == 2,
                $"loop tag came out as {tags[1]}");
            Assert.True((string?)tags[1]["direction"] == "pingpong",
                $"direction should use Aseprite's spelling, got {tags[1]["direction"]}");
        });

        t.Check("a per-frame duration override reaches the JSON, others keep the default", () =>
        {
            var frames = (JObject)JObject.Parse(taggedJson)["frames"]!;
            Assert.True((int)frames["test 1"]!["duration"]! == 400,
                $"frame 1 duration {frames["test 1"]!["duration"]}, expected 400");
            Assert.True((int)frames["test 0"]!["duration"]! == expectedDuration,
                $"frame 0 should keep the {expectedDuration} ms default");
        });

        t.Check("pivot + 9-slice become an Aseprite slice with a centre rect", () =>
        {
            var slices = (JArray)JObject.Parse(taggedJson)["meta"]!["slices"]!;
            Assert.True(slices.Count == 1, $"expected one slice, got {slices.Count}");

            var key = slices[0]["keys"]![0]!;
            Assert.True((int)key["pivot"]!["x"]! == 32 && (int)key["pivot"]!["y"]! == 60, $"pivot came out as {key["pivot"]}");
            // center = (Left, Top, W-L-R, H-T-B) — the shape engine importers read as the 9-slice inner rect.
            Assert.True((int)key["center"]!["x"]! == 8 && (int)key["center"]!["y"]! == 6
                        && (int)key["center"]!["w"]! == canvasW - 16 && (int)key["center"]!["h"]! == canvasH - 12,
                $"center came out as {key["center"]}");
        });

        t.Check("an --tag export packs only that range and re-bases it to frame 0", () =>
        {
            using var filtered = SpriteSheetBuilder.Build(sprite, 1, new SpriteSheetOptions
            {
                PackMode = SheetPackMode.Grid,
                MaxColumns = 4,
                TagFilter = "loop",
                SpriteName = "test",
                ImageFileName = "test.png"
            });

            Assert.True(filtered.Frames.Count == 2, $"'loop' covers 2 frames, packed {filtered.Frames.Count}");

            var doc = JObject.Parse(new AsepriteJsonEmitter().Emit(filtered, new SheetMetadataOptions()));
            var tags = (JArray)doc["meta"]!["frameTags"]!;
            Assert.True(tags.Count == 1 && (string?)tags[0]["name"] == "loop",
                "a filtered export should carry only the filtered tag");
            Assert.True((int)tags[0]["from"]! == 0 && (int)tags[0]["to"]! == 1,
                $"the tag must be re-based onto the exported sheet, got {tags[0]["from"]}..{tags[0]["to"]}");

            // Frame keys are re-based too, and the animations map has to resolve against them.
            var frames = (JObject)doc["frames"]!;
            Assert.True(frames.ContainsKey("test 0") && frames.ContainsKey("test 1"),
                $"expected re-based frame keys, got [{string.Join(", ", frames.Properties().Select(p => p.Name))}]");
            Assert.True(((JArray)doc["animations"]!["loop"]!).Count == 2,
                "the animations map should list both frames of the filtered tag");

            // The 400 ms override lives on source frame 1 = first frame of this range.
            Assert.True((int)frames["test 0"]!["duration"]! == 400,
                $"durations must follow the SOURCE frame, got {frames["test 0"]!["duration"]}");
        });

        t.Check("an unknown --tag is a named error, not an empty sheet", () =>
        {
            var threw = false;
            try
            {
                using var _ = SpriteSheetBuilder.Build(sprite, 1, new SpriteSheetOptions { TagFilter = "nope" });
            }
            catch (ArgumentException e) when (e.Message.Contains("nope") && e.Message.Contains("loop"))
            {
                threw = true;
            }

            Assert.True(threw, "expected an ArgumentException naming the missing tag and listing the available ones");
        });
    }

    // --- Scenario 6b: engine export presets (H2.2 PR-4) --------------------------------------------
    // Godot .tres / Unity .png.meta / libGDX .atlas over the SAME PackedSheet the Aseprite emitter
    // consumes. The checks target what each format gets *wrong by default* rather than re-asserting the
    // packer: Godot's relative frame duration, Unity's bottom-up rect origin and id stability, and
    // libGDX's bottom-left trim offset + name/index animation convention.
    static void EnginePresetExportScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Engine export presets scenario (Godot / Unity / libGDX) ===");

        // Two rows on purpose (3 frames, 2 columns): a single-row sheet cannot distinguish a top-down
        // rect from a bottom-up one, which is the whole point of the Unity y check below.
        h.NewProject(64);
        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Red);
        h.DrawPixel(5, 5);
        h.Exec("Sprite.Animation.AddFrame");
        h.DrawPixel(10, 10);
        h.Exec("Sprite.Animation.AddFrame");
        h.DrawPixel(20, 20);

        var sprite = h.ActiveSprite;
        var canvasW = (int)sprite.Size.Width;
        var canvasH = (int)sprite.Size.Height;
        var fps = sprite.FrameRate;

        sprite.AnimationTags =
        [
            new SpriteAnimationTag { Name = "intro", From = 0, To = 0 },
            new SpriteAnimationTag { Name = "loop", From = 1, To = 2 }
        ];
        sprite.SetFrameDurationMs(1, 400);
        sprite.NineSlice = new NineSliceMargins { Left = 8, Top = 6, Right = 4, Bottom = 2 };

        SpriteSheetOptions Opts(bool trim = false) => new()
        {
            PackMode = trim ? SheetPackMode.Tight : SheetPackMode.Grid,
            MaxColumns = 2,
            Trim = trim,
            SpriteName = "hero",
            ImageFileName = "hero.png"
        };

        using var sheet = SpriteSheetBuilder.Build(sprite, 1, Opts());
        using var trimmed = SpriteSheetBuilder.Build(sprite, 1, Opts(trim: true));

        var opts = new SheetMetadataOptions { AppVersion = "9.9.9" };

        // --- registry wiring: the UI dropdown and CLI --format both enumerate this list ------------
        t.Check("all four metadata presets are registered and resolvable by id", () =>
        {
            foreach (var id in new[] { "aseprite", "godot", "unity", "libgdx" })
                Assert.True(SheetMetadataEmitters.TryGet(id) != null, $"emitter '{id}' is not registered");

            Assert.True(SheetMetadataEmitters.TryGet("nope") == null, "an unknown id must resolve to null");
            Assert.True(SheetMetadataEmitters.TryGet("none") == null, "'none' means no sidecar");
            Assert.True(SheetMetadataEmitters.All.Select(e => e.Id).Distinct().Count() == SheetMetadataEmitters.All.Count,
                "emitter ids must be unique — the CLI and the UI both key on them");
            Assert.True(SheetMetadataEmitters.All.All(e => e.FileExtension.StartsWith('.')),
                "every FileExtension must carry its dot (Path.ChangeExtension relies on it)");
        });

        // --- Godot -------------------------------------------------------------------------------
        var tres = new GodotSpriteFramesEmitter().Emit(sheet, opts);
        Console.WriteLine("  [diag] godot head: " + tres.Replace("\r", "").Replace("\n", " ")[..Math.Min(120, tres.Length)]);

        t.Check("Godot .tres declares a format=3 SpriteFrames with one AtlasTexture per frame", () =>
        {
            Assert.True(tres.StartsWith("[gd_resource type=\"SpriteFrames\"", StringComparison.Ordinal),
                "missing the gd_resource header");
            Assert.True(tres.Contains("format=3"), "Godot 4 text resources are format=3");

            var subs = Regex.Matches(tres, @"\[sub_resource type=""AtlasTexture"" id=""([^""]+)""\]").Count;
            Assert.True(subs == 3, $"expected 3 AtlasTexture sub-resources, got {subs}");

            // load_steps counts 1 ext_resource + N sub_resources + the resource itself.
            var steps = int.Parse(Regex.Match(tres, @"load_steps=(\d+)").Groups[1].Value);
            Assert.True(steps == subs + 2, $"load_steps={steps}, expected {subs + 2}");

            Assert.True(Regex.Matches(tres, @"\[ext_resource ").Count == 1, "expected exactly one texture ext_resource");
            Assert.True(tres.Contains("path=\"res://hero.png\""), "the ext_resource must point at the sheet PNG");
        });

        t.Check("Godot regions match the packed frame rects", () =>
        {
            var regions = Regex.Matches(tres, @"region = Rect2\((\d+), (\d+), (\d+), (\d+)\)")
                .Select(m => new SKRectI(
                    int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[2].Value) + int.Parse(m.Groups[4].Value)))
                .ToArray();

            foreach (var frame in sheet.Frames)
                Assert.True(regions.Contains(frame.Frame), $"no region matches packed frame {frame.Index} at {frame.Frame}");
        });

        t.Check("Godot emits one animation per tag, named as the tag", () =>
        {
            var names = Regex.Matches(tres, @"""name"": &""([^""]+)""").Select(m => m.Groups[1].Value).ToArray();
            Assert.True(names.Length == 2, $"expected 2 animations, got [{string.Join(", ", names)}]");
            Assert.True(names.Contains("intro") && names.Contains("loop"),
                $"animations should be named after the tags, got [{string.Join(", ", names)}]");
            Assert.True(tres.Contains($"\"speed\": {fps.ToString("0.####", CultureInfo.InvariantCulture)}")
                        || tres.Contains($"\"speed\": {(int)fps}.0"),
                "the animation speed should be the sprite's frame rate");
        });

        // The discriminating check for the duration snap: Godot's per-frame `duration` is a MULTIPLIER of
        // the animation speed, and the sprite's own default is stored as whole ms (15 fps -> 67), so a naive
        // ms*fps/1000 yields 1.005 for an untouched frame and plays the animation slow.
        t.Check("Godot durations are 1.0 for default frames and scaled for an override", () =>
        {
            var durations = Regex.Matches(tres, @"""duration"": ([0-9.]+)")
                .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .ToArray();

            Assert.True(durations.Length == 3, $"expected 3 frame durations, got {durations.Length}");
            Assert.True(durations.Count(d => Math.Abs(d - 1.0) < 1e-9) == 2,
                $"the two default frames must be exactly 1.0, got [{string.Join(", ", durations)}]");

            var expectedOverride = 400.0 * fps / 1000.0;
            Assert.True(durations.Any(d => Math.Abs(d - expectedOverride) < 0.01),
                $"the 400 ms override should scale to ~{expectedOverride:0.###}, got [{string.Join(", ", durations)}]");
        });

        t.Check("Godot restores a trimmed frame's footprint through AtlasTexture.margin", () =>
        {
            var tresTrimmed = new GodotSpriteFramesEmitter().Emit(trimmed, opts);
            var margins = Regex.Matches(tresTrimmed, @"margin = Rect2\((\d+), (\d+), (\d+), (\d+)\)").ToArray();
            Assert.True(margins.Length == 3, $"expected a margin on each trimmed frame, got {margins.Length}");

            // margin.size is what was cropped away, so region.size + margin.size == the original canvas.
            foreach (var m in margins)
            {
                var mw = int.Parse(m.Groups[3].Value);
                var mh = int.Parse(m.Groups[4].Value);
                Assert.True(mw > 0 && mh > 0, $"a single-pixel frame must crop in both axes, got {mw}x{mh}");
            }

            var first = trimmed.Frames[0];
            Assert.True(tresTrimmed.Contains(
                    $"margin = Rect2({first.SpriteSourceRect.Left}, {first.SpriteSourceRect.Top}, " +
                    $"{first.SourceSize.Width - first.Frame.Width}, {first.SourceSize.Height - first.Frame.Height})"),
                "the margin must be (trim offset, cropped size)");

            Assert.True(!tres.Contains("margin = "), "an untrimmed export must not emit a margin");
        });

        t.Check("Godot falls back to a 'default' animation when the sprite has no tags", () =>
        {
            var untagged = sprite.AnimationTags;
            sprite.AnimationTags = null;
            try
            {
                using var plain = SpriteSheetBuilder.Build(sprite, 1, Opts());
                var doc = new GodotSpriteFramesEmitter().Emit(plain, opts);
                var names = Regex.Matches(doc, @"""name"": &""([^""]+)""").Select(m => m.Groups[1].Value).ToArray();
                Assert.True(names.Length == 1 && names[0] == "default",
                    $"expected a single 'default' animation, got [{string.Join(", ", names)}]");
                Assert.True(Regex.Matches(doc, @"""texture"": SubResource").Count == 3,
                    "the default animation must cover every frame");
            }
            finally
            {
                sprite.AnimationTags = untagged;
            }
        });

        // --- Unity -------------------------------------------------------------------------------
        var meta = new UnityMetaEmitter().Emit(sheet, opts);

        t.Check("Unity .meta is a Multiple-mode TextureImporter with pixel-art defaults", () =>
        {
            Assert.True(meta.StartsWith("fileFormatVersion: 2", StringComparison.Ordinal), "missing the meta header");
            Assert.True(Regex.IsMatch(meta, @"^guid: [0-9a-f]{32}$", RegexOptions.Multiline),
                "guid must be 32 lowercase hex chars");
            Assert.True(meta.Contains("  spriteMode: 2\n"), "spriteMode must be 2 (Multiple) for the sprite list to apply");
            Assert.True(meta.Contains("    filterMode: 0\n"), "pixel art needs Point filtering");
            Assert.True(meta.Contains("    textureCompression: 0\n"), "pixel art must not be compressed");
            Assert.True(meta.Contains("  spriteMeshType: 0\n"), "FullRect is required for 9-slice borders");
            Assert.True(new UnityMetaEmitter().FileExtension == ".png.meta",
                "Unity's sidecar keeps the image extension: <image>.png.meta");
        });

        t.Check("Unity emits one sprite per animation frame, named <tag>_<index>", () =>
        {
            var names = Regex.Matches(meta, @"^      name: (.+)$", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).ToArray();

            Assert.True(names.Length == 3, $"expected 3 sprites, got [{string.Join(", ", names)}]");
            Assert.True(names.Contains("intro_0") && names.Contains("loop_0") && names.Contains("loop_1"),
                $"names should be tag-scoped and 0-based within the tag, got [{string.Join(", ", names)}]");
            Assert.True(names.Distinct().Count() == names.Length, "sprite names must be unique or Unity drops one");
        });

        // The classic way a hand-written .meta comes out wrong: Unity rects are bottom-up.
        t.Check("Unity sprite rects flip Y into Unity's bottom-up texture space", () =>
        {
            var rects = Regex.Matches(meta, @"        x: (\d+)\n        y: (\d+)\n        width: (\d+)\n        height: (\d+)")
                .Select(m => (X: int.Parse(m.Groups[1].Value), Y: int.Parse(m.Groups[2].Value),
                              W: int.Parse(m.Groups[3].Value), H: int.Parse(m.Groups[4].Value)))
                .ToArray();

            Assert.True(rects.Length == 3, $"expected 3 rects, got {rects.Length}");
            Assert.True(sheet.Image.Height > canvasH,
                "this check needs a multi-row sheet to be meaningful — MaxColumns=2 over 3 frames");

            foreach (var frame in sheet.Frames)
            {
                var expectedY = sheet.Image.Height - frame.Frame.Top - frame.Frame.Height;
                Assert.True(rects.Any(r => r.X == frame.Frame.Left && r.Y == expectedY
                                           && r.W == frame.Frame.Width && r.H == frame.Frame.Height),
                    $"frame {frame.Index} at top={frame.Frame.Top} should map to y={expectedY}; " +
                    $"rects were [{string.Join(", ", rects.Select(r => $"({r.X},{r.Y})"))}]");
            }

            // A top-row frame must end up with the LARGEST y — the assertion a top-down emit fails.
            var topRow = sheet.Frames.Where(f => f.Frame.Top == 0).ToArray();
            Assert.True(topRow.Length > 0, "expected at least one frame on the top row");
            var maxY = rects.Max(r => r.Y);
            Assert.True(topRow.All(f => sheet.Image.Height - f.Frame.Top - f.Frame.Height == maxY),
                "top-row frames must carry the largest Unity y");
        });

        t.Check("Unity maps the 9-slice onto border as (left, bottom, right, top)", () =>
        {
            var border = Regex.Match(meta, @"      border: \{x: (\d+), y: (\d+), z: (\d+), w: (\d+)\}");
            Assert.True(border.Success, "no sprite border emitted");
            Assert.True(border.Groups[1].Value == "8", $"border.x should be Left=8, got {border.Groups[1].Value}");
            Assert.True(border.Groups[2].Value == "2", $"border.y should be Bottom=2, got {border.Groups[2].Value}");
            Assert.True(border.Groups[3].Value == "4", $"border.z should be Right=4, got {border.Groups[3].Value}");
            Assert.True(border.Groups[4].Value == "6", $"border.w should be Top=6, got {border.Groups[4].Value}");
        });

        // Re-exporting over an existing asset must not orphan scene references.
        t.Check("Unity ids are stable across re-export and unique per sprite", () =>
        {
            var again = new UnityMetaEmitter().Emit(sheet, opts);
            Assert.True(again == meta, "the same sheet must emit a byte-identical .meta (ids must not be random)");

            var spriteIds = Regex.Matches(meta, @"      spriteID: ([0-9a-f]{32})").Select(m => m.Groups[1].Value).ToArray();
            Assert.True(spriteIds.Length == 3 && spriteIds.Distinct().Count() == 3,
                $"expected 3 distinct spriteIDs, got [{string.Join(", ", spriteIds)}]");

            var internalIds = Regex.Matches(meta, @"      internalID: (-?\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
            Assert.True(internalIds.Length == 3 && internalIds.Distinct().Count() == 3,
                "internalIDs must be distinct within one asset");
            Assert.True(internalIds.All(i => i > 0), "internalID 0 means 'unset' and negatives confuse the importer");

            // Each internalID must also appear in the name table Unity resolves sub-assets through.
            foreach (var id in internalIds)
                Assert.True(meta.Contains($"      213: {id}\n"), $"internalID {id} is missing from internalIDToNameTable");
        });

        // --- libGDX ------------------------------------------------------------------------------
        var atlas = new LibGdxAtlasEmitter().Emit(sheet, opts);

        t.Check("libGDX .atlas page header names the sheet and uses Nearest filtering", () =>
        {
            var lines = atlas.Replace("\r", "").Split('\n');
            Assert.True(lines[0] == "hero.png", $"first line must be the image file name, got '{lines[0]}'");
            Assert.True(lines[1] == $"size: {sheet.Image.Width},{sheet.Image.Height}", $"got '{lines[1]}'");
            Assert.True(lines[2] == "format: RGBA8888", $"got '{lines[2]}'");
            Assert.True(lines[3] == "filter: Nearest,Nearest", $"pixel art must not be filtered, got '{lines[3]}'");
            Assert.True(lines[4] == "repeat: none", $"got '{lines[4]}'");
        });

        // atlas.findRegions("loop") is ordered by index — this naming IS the animation in libGDX.
        t.Check("libGDX repeats the tag name per frame with a 0-based index within the tag", () =>
        {
            var regions = Regex.Matches(atlas, @"^(\S.*)\n  rotate: false\n  xy: (\d+), (\d+)\n  size: (\d+), (\d+)\n  orig: (\d+), (\d+)\n  offset: (\d+), (\d+)\n  index: (\d+)$",
                    RegexOptions.Multiline)
                .Select(m => (Name: m.Groups[1].Value, Index: int.Parse(m.Groups[10].Value),
                              X: int.Parse(m.Groups[2].Value), Y: int.Parse(m.Groups[3].Value)))
                .ToArray();

            Assert.True(regions.Length == 3, $"expected 3 fully-formed regions, got {regions.Length}");

            var loop = regions.Where(r => r.Name == "loop").OrderBy(r => r.Index).ToArray();
            Assert.True(loop.Length == 2, $"'loop' covers 2 frames, got {loop.Length}");
            Assert.True(loop[0].Index == 0 && loop[1].Index == 1,
                $"indices must restart per tag, got [{string.Join(", ", loop.Select(r => r.Index))}]");

            var intro = regions.Where(r => r.Name == "intro").ToArray();
            Assert.True(intro.Length == 1 && intro[0].Index == 0, "'intro' should be a single index-0 region");
        });

        t.Check("libGDX trim offset is measured from the bottom-left of the original frame", () =>
        {
            var atlasTrimmed = new LibGdxAtlasEmitter().Emit(trimmed, opts);
            var frame = trimmed.Frames[0];
            var expectedY = frame.SourceSize.Height - frame.SpriteSourceRect.Top - frame.Frame.Height;

            Assert.True(expectedY != frame.SpriteSourceRect.Top,
                "this check is vacuous unless the top and bottom margins differ — pick a different pixel");
            Assert.True(atlasTrimmed.Contains($"  offset: {frame.SpriteSourceRect.Left}, {expectedY}\n"),
                $"expected offset {frame.SpriteSourceRect.Left}, {expectedY} (bottom-up) — a top-down emit " +
                $"would write {frame.SpriteSourceRect.Left}, {frame.SpriteSourceRect.Top}");
            Assert.True(atlasTrimmed.Contains($"  orig: {frame.SourceSize.Width}, {frame.SourceSize.Height}\n"),
                "orig must stay the untrimmed source size");
        });

        t.Check("libGDX uses the sprite name as the region name when there are no tags", () =>
        {
            var untagged = sprite.AnimationTags;
            sprite.AnimationTags = null;
            try
            {
                using var plain = SpriteSheetBuilder.Build(sprite, 1, Opts());
                var doc = new LibGdxAtlasEmitter().Emit(plain, opts);
                var names = Regex.Matches(doc, @"^(\S.*)\n  rotate:", RegexOptions.Multiline)
                    .Select(m => m.Groups[1].Value).Distinct().ToArray();
                Assert.True(names.Length == 1 && names[0] == "hero",
                    $"expected a single 'hero' region name, got [{string.Join(", ", names)}]");
            }
            finally
            {
                sprite.AnimationTags = untagged;
            }
        });

        // --- partial tag coverage: the presets must not silently lose frames ----------------------
        // A tag covering only part of the timeline is ordinary. Unity and libGDX represent plain regions, so
        // an uncovered frame with no entry is unreachable (unsliceable / invisible to findRegion); Godot has
        // no free-standing frame at all, so it drops them on purpose. The three must differ deliberately.
        t.Check("partial tag coverage still addresses every frame in Unity and libGDX", () =>
        {
            var full = sprite.AnimationTags;
            // Frames 1 and 2 are left uncovered.
            sprite.AnimationTags = [new SpriteAnimationTag { Name = "intro", From = 0, To = 0 }];
            try
            {
                using var partial = SpriteSheetBuilder.Build(sprite, 1, Opts());
                var frameCount = partial.Frames.Count;
                Assert.True(frameCount == 3, $"the sheet should still pack all 3 frames, got {frameCount}");

                var atlas = new LibGdxAtlasEmitter().Emit(partial, opts);
                var regionLines = Regex.Matches(atlas, @"^(\S.*)\n  rotate:", RegexOptions.Multiline).Count;
                Assert.True(regionLines == frameCount,
                    $"libGDX emitted {regionLines} regions for {frameCount} packed frames — an uncovered " +
                    "frame has no region and can never be reached through findRegion");

                var meta = new UnityMetaEmitter().Emit(partial, opts);
                var spriteRects = Regex.Matches(meta, @"^      internalID: ", RegexOptions.Multiline).Count;
                Assert.True(spriteRects == frameCount,
                    $"Unity emitted {spriteRects} sprite rects for {frameCount} packed frames — an uncovered " +
                    "frame cannot be sliced, and hand-slicing is overwritten on the next re-export");

                // Godot's opposite, deliberate choice.
                var tres = new GodotSpriteFramesEmitter().Emit(partial, opts);
                var atlasTextures = Regex.Matches(tres, @"^\[sub_resource ", RegexOptions.Multiline).Count;
                Assert.True(atlasTextures == 1,
                    $"Godot should emit only the referenced frame's AtlasTexture, got {atlasTextures}");
            }
            finally
            {
                sprite.AnimationTags = full;
            }
        });

        t.Check("Godot sub-resource ids are unique per frame", () =>
        {
            var tres = new GodotSpriteFramesEmitter().Emit(sheet, opts);
            var ids = Regex.Matches(tres, @"\[sub_resource type=""AtlasTexture"" id=""([^""]+)""\]")
                .Select(m => m.Groups[1].Value).ToArray();

            Assert.True(ids.Length > 0, "no AtlasTexture sub-resources were emitted");
            Assert.True(ids.Distinct().Count() == ids.Length,
                $"duplicate sub-resource ids [{string.Join(", ", ids)}] — Godot keeps only the LAST " +
                "[sub_resource] with a given id, so two frames would silently show identical pixels");
        });

        t.Check("Unity internalIDs are unique within the asset", () =>
        {
            var meta = new UnityMetaEmitter().Emit(sheet, opts);
            var ids = Regex.Matches(meta, @"^      internalID: (-?\d+)", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).ToArray();

            Assert.True(ids.Length > 0, "no sprite internalIDs were emitted");
            Assert.True(ids.Distinct().Count() == ids.Length,
                $"duplicate internalIDs [{string.Join(", ", ids)}] — Unity rejects them within one asset");
        });

        // Two tags that differ only in a character Sanitize strips would collide on one region name, and
        // libGDX's index sort would then interleave both animations into an arbitrary play order.
        t.Check("libGDX disambiguates tag names that sanitize to the same region name", () =>
        {
            var full = sprite.AnimationTags;
            sprite.AnimationTags =
            [
                new SpriteAnimationTag { Name = "run", From = 0, To = 0 },
                new SpriteAnimationTag { Name = "run:", From = 1, To = 1 }
            ];
            try
            {
                using var packed = SpriteSheetBuilder.Build(sprite, 1, Opts());
                var atlas = new LibGdxAtlasEmitter().Emit(packed, opts);
                var names = Regex.Matches(atlas, @"^(\S.*)\n  rotate:", RegexOptions.Multiline)
                    .Select(m => m.Groups[1].Value).ToArray();

                Assert.True(names.Distinct().Count() == names.Count(),
                    $"two animations share the region name [{string.Join(", ", names)}] — findRegions would " +
                    "interleave them and the play order becomes arbitrary");
            }
            finally
            {
                sprite.AnimationTags = full;
            }
        });

        // The 9-slice margins are canvas-space; a trimmed frame's rect starts at SpriteSourceRect, so
        // copying them across unchanged misplaces every slice line by exactly the trim offset.
        t.Check("Unity 9-slice borders are re-based onto a trimmed frame's rect", () =>
        {
            var meta = new UnityMetaEmitter().Emit(trimmed, opts);
            var borders = Regex.Matches(meta, @"^      border: \{x: (-?\d+), y: (-?\d+), z: (-?\d+), w: (-?\d+)\}",
                    RegexOptions.Multiline)
                .Select(m => (X: int.Parse(m.Groups[1].Value), Y: int.Parse(m.Groups[2].Value),
                              Z: int.Parse(m.Groups[3].Value), W: int.Parse(m.Groups[4].Value)))
                .ToArray();

            Assert.True(borders.Length > 0, "no borders were emitted");

            var frames = trimmed.Frames.OrderBy(f => f.Index).ToArray();
            Assert.True(frames.Any(f => f.Trimmed),
                "this check is vacuous unless the packer actually trimmed something");

            foreach (var b in borders)
            {
                Assert.True(b.X >= 0 && b.Y >= 0 && b.Z >= 0 && b.W >= 0,
                    $"a re-based border went negative: {b}");
            }

            // The canvas margins are 8/6/4/2; on a trimmed frame at least one side must have shrunk, or the
            // re-basing did not happen at all.
            Assert.True(borders.Any(b => b != (8, 2, 4, 6)),
                $"every trimmed frame kept the raw canvas border (8,2,4,6) — the trim offset was not applied");
        });
    }

    // --- Scenario 6c: .piskel import (H2.3) --------------------------------------------------------
    // The document is synthesized here rather than checked in as a fixture, so the exact structure the
    // checks depend on is visible next to them — in particular the layout indirection, which is the one
    // part of the format a reader gets wrong silently.
    static void PiskelImportScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== .piskel import scenario ===");

        const int W = 4, H = 4;

        // Encodes a horizontal strip of WxH cells as a base64 data-uri PNG, the way Piskel stores a chunk.
        static string Sheet(params SKColor?[] cells)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(W * cells.Length, H, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                for (var i = 0; i < cells.Length; i++)
                {
                    if (cells[i] is not { } color)
                        continue;
                    using var paint = new SKPaint { Color = color };
                    canvas.DrawRect(new SKRect(i * W, 0, i * W + W, H), paint);
                }
            }

            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
        }

        // Layers are JSON *strings* inside the envelope — that double encoding is part of the format.
        static string LayerJson(string name, double opacity, int frameCount, string layout, string sheet) =>
            JsonConvert.SerializeObject(
                $"{{\"name\":\"{name}\",\"opacity\":{opacity.ToString(CultureInfo.InvariantCulture)}," +
                $"\"frameCount\":{frameCount},\"chunks\":[{{\"layout\":{layout},\"base64PNG\":\"{sheet}\"}}]}}");

        // Layer 0 "Background": cell 0 (red) fills frames 0 AND 2, cell 1 (green) fills frame 1.
        //   A reader that treats cell i as frame i produces red/green/EMPTY instead of red/green/red.
        // Layer 1 "Overlay" at 50 %: cells fill frames 0 and 2, leaving frame 1 uncovered (= transparent).
        var doc = "{\"modelVersion\":2,\"piskel\":{\"name\":\"hero\",\"fps\":8,\"width\":" + W + ",\"height\":" + H +
                  ",\"layers\":[" +
                  LayerJson("Background", 1.0, 3, "[[0,2],[1]]", Sheet(SKColors.Red, SKColors.Lime)) + "," +
                  LayerJson("Overlay", 0.5, 3, "[[0],[2]]", Sheet(SKColors.Blue, SKColors.Blue)) +
                  "]}}";

        // --- the pure reader ----------------------------------------------------------------------
        t.Check("piskel: the envelope, the double-encoded layers and the canvas size are read", () =>
        {
            var parsed = PiskelDocument.Parse(doc);
            Assert.True(parsed.Width == W && parsed.Height == H, $"size came out {parsed.Width}x{parsed.Height}");
            Assert.True(parsed.Layers.Count == 2, $"expected 2 layers, got {parsed.Layers.Count}");
            Assert.True(parsed.FrameCount == 3, $"expected 3 frames, got {parsed.FrameCount}");
            Assert.True(Math.Abs(parsed.Fps - 8f) < 0.01f, $"fps came out {parsed.Fps}");
            Assert.True(parsed.Layers[0].Name == "Background" && parsed.Layers[1].Name == "Overlay",
                $"layer names came out [{string.Join(", ", parsed.Layers.Select(l => l.Name))}]");
        });

        t.Check("piskel: an inline layer object parses as well as the string form", () =>
        {
            var inline = "{\"modelVersion\":2,\"piskel\":{\"name\":\"x\",\"width\":" + W + ",\"height\":" + H +
                         ",\"layers\":[{\"name\":\"Inline\",\"opacity\":1,\"frameCount\":1," +
                         "\"chunks\":[{\"layout\":[[0]],\"base64PNG\":\"" + Sheet(SKColors.Red) + "\"}]}]}}";
            var parsed = PiskelDocument.Parse(inline);
            Assert.True(parsed.Layers.Count == 1 && parsed.Layers[0].Name == "Inline",
                "an inlined layer object should be accepted too");
        });

        var data = PiskelImporter.BuildImportData(doc);

        t.Check("piskel: layers keep their name and opacity, and every layer gets every frame", () =>
        {
            Assert.True(data.Size.Width == W && data.Size.Height == H, $"import size {data.Size}");
            Assert.True(data.Layers.Count == 2, $"expected 2 import layers, got {data.Layers.Count}");
            Assert.True(data.Layers[0].Name == "Background" && data.Layers[1].Name == "Overlay",
                "layer names must survive the conversion");
            Assert.True(Math.Abs(data.Layers[1].Opacity - 0.5f) < 0.01f,
                $"Overlay opacity came out {data.Layers[1].Opacity}");
            // A short layer would leave the sprite's timeline ragged, so both are padded to 3.
            Assert.True(data.Layers.All(l => l.Frames.Count == 3),
                $"frame counts came out [{string.Join(", ", data.Layers.Select(l => l.Frames.Count))}]");
        });

        static SKColor PixelOf(LayerFrameInfo frame) => frame.BitmapProviderFunc!.Invoke().GetPixel(1, 1);

        // THE discriminating check for the layout indirection.
        t.Check("piskel: a cell shared by several frames fills all of them (layout, not cell order)", () =>
        {
            var bg = data.Layers[0].Frames;
            Assert.True(PixelOf(bg[0]) == SKColors.Red, $"frame 0 should be red, got {PixelOf(bg[0])}");
            Assert.True(PixelOf(bg[1]) == SKColors.Lime, $"frame 1 should be green, got {PixelOf(bg[1])}");
            Assert.True(PixelOf(bg[2]) == SKColors.Red,
                $"frame 2 shares cell 0 with frame 0 and must also be red, got {PixelOf(bg[2])} " +
                "(reading cell i as frame i leaves this frame empty)");
        });

        t.Check("piskel: a frame no layout covers comes in transparent, not dropped", () =>
        {
            var overlay = data.Layers[1].Frames;
            Assert.True(PixelOf(overlay[0]) == SKColors.Blue, $"overlay frame 0 should be blue, got {PixelOf(overlay[0])}");
            Assert.True(PixelOf(overlay[1]).Alpha == 0, $"overlay frame 1 should be empty, got {PixelOf(overlay[1])}");
            Assert.True(PixelOf(overlay[2]) == SKColors.Blue, $"overlay frame 2 should be blue, got {PixelOf(overlay[2])}");
        });

        // Frames become independently editable layer bitmaps, so a shared cell must not share a buffer.
        t.Check("piskel: frames sharing a cell do not share one bitmap", () =>
        {
            var fresh = PiskelImporter.BuildImportData(doc);
            var bg = fresh.Layers[0].Frames;
            var first = bg[0].BitmapProviderFunc!.Invoke();
            var third = bg[2].BitmapProviderFunc!.Invoke();

            Assert.True(!ReferenceEquals(first, third), "frames 0 and 2 must be distinct bitmap instances");
            first.SetPixel(1, 1, SKColors.Black);
            Assert.True(third.GetPixel(1, 1) == SKColors.Red,
                $"editing frame 0 changed frame 2 — they share a buffer (frame 2 now {third.GetPixel(1, 1)})");
        });

        // --- malformed input ----------------------------------------------------------------------
        t.Check("piskel: a v1 document is refused with a message naming the version", () =>
        {
            var v1 = doc.Replace("\"modelVersion\":2", "\"modelVersion\":1");
            var threw = false;
            try { PiskelDocument.Parse(v1); }
            catch (FormatException e) when (e.Message.Contains("1") && e.Message.Contains("version")) { threw = true; }
            Assert.True(threw, "expected a FormatException naming the unsupported model version");
        });

        t.Check("piskel: garbage and empty input are format errors, not crashes", () =>
        {
            foreach (var bad in new[] { "", "   ", "not json at all", "{}", "{\"piskel\":{}}" })
            {
                var threw = false;
                try { PiskelDocument.Parse(bad); }
                catch (FormatException) { threw = true; }
                Assert.True(threw, $"expected a FormatException for input '{bad}'");
            }
        });

        // --- registration + the real flow ---------------------------------------------------------
        t.Check("piskel: .piskel is a registered importable extension", () =>
        {
            var importService = h.Services.GetRequiredService<IImportService>();
            Assert.True(importService.SupportedExtensions.Contains(".piskel"),
                $"no importer registered for .piskel; registered: [{string.Join(", ", importService.SupportedExtensions)}]");
            Assert.True(ExportImportProjectType.GetSupportedImportFileExtensions().Contains(".piskel"),
                "the file picker's extension list must offer .piskel");
        });

        t.Check("piskel: the analyzer classifies it as a layered document, not a raster still", () =>
        {
            var files = h.Services.GetRequiredService<IFileService>();
            var dir = Path.Combine(Path.GetTempPath(), "pix2d-piskel-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            try
            {
            var path = Path.Combine(dir, "hero.piskel");
            File.WriteAllText(path, doc);

            var source = new Pix2d.Common.FileSystem.NetFileSource(path);
            Assert.True(ImportAnalyzer.ClassifyKind([source]) == ImportFileKind.LayeredDocument,
                $"classified as {ImportAnalyzer.ClassifyKind([source])}");

            // And end-to-end through the real flow: one file becomes one artboard with both layers.
            h.NewProject(16);
            var before = h.ArtboardCount;
            var flow = h.Services.GetRequiredService<IImportFlowService>();

            // Off the main thread, like RunExport: the flow's awaits would otherwise post continuations to
            // Avalonia's dispatcher, which never pumps in this synchronous harness.
            var task = Task.Run(() => flow.RunImportFlowAsync(new ImportRequest([source], null, FromDrag: false)));
            Assert.True(task.Wait(TimeSpan.FromSeconds(30)), "the import flow did not complete");
            Assert.True(task.Result.Success, "import flow failed: " + task.Result.Message);

            Assert.True(h.ArtboardCount == before + 1, $"expected one new artboard, count went {before} -> {h.ArtboardCount}");

            var imported = h.Artboards.Last();
            var layers = imported.Nodes.OfType<Pix2dSprite.Layer>().ToArray();
            Assert.True(layers.Length == 2, $"expected 2 layers on the imported sprite, got {layers.Length}");
            Assert.True(imported.GetFramesCount() == 3, $"expected 3 frames, got {imported.GetFramesCount()}");
            Assert.True((int)imported.Size.Width == W && (int)imported.Size.Height == H,
                $"the sprite should take the document's canvas size, got {imported.Size}");

            // SpriteImportApplier has to carry the name and opacity onto the real layers, not just parse them.
            Assert.True(layers.Any(l => l.Name == "Background") && layers.Any(l => l.Name == "Overlay"),
                $"layer names did not reach the sprite: [{string.Join(", ", layers.Select(l => l.Name))}]");
            var overlay = layers.First(l => l.Name == "Overlay");
            Assert.True(Math.Abs(overlay.Opacity - 0.5f) < 0.01f,
                $"the Overlay layer's opacity did not reach the sprite (got {overlay.Opacity})");

            // EVERY layer must hold exactly the document's frames — not just the first one. Pix2dSprite
            // .AddLayer seeds a new layer with the first layer's frame count, which is already 3 by the time
            // layer 2 is added, and InsertFrameFromBitmap *inserts*: deleting a single frame before filling
            // left layer 2 with 3 real frames plus 2 stale empties. Layers then disagree on FrameCount, the
            // empties are saved into the .pix2d, and later frame edits land on desynced indices. A sprite-wide
            // GetFramesCount() check cannot see this — it reports one layer's view.
            foreach (var layer in layers)
            {
                Assert.True(layer.FrameCount == 3,
                    $"layer '{layer.Name}' has {layer.FrameCount} frames, expected 3 (stale frames left behind)");
            }

            // The document's fps must reach the sprite rather than silently falling back to the 15 default.
            Assert.True(Math.Abs(imported.FrameRate - 8f) < 0.01f,
                $"the document's frame rate did not reach the sprite (got {imported.FrameRate})");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        });
    }

    // --- Scenario 6d: linked cels (H2.4) -----------------------------------------------------------
    // The model already shared SpriteNodes between frames as an invisible memory optimisation, broken by
    // copy-on-write on the first edit. Linked cels make that sharing deliberate, so the checks that matter
    // are the ones separating the two behaviours: an edit must PROPAGATE across a link and must still SPLIT
    // an old incidentally-shared duplicate.
    static void LinkedCelsScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Linked cels scenario ===");

        // Builds a fresh 3-frame sprite with a distinct pixel per frame and returns its selected layer.
        Pix2dSprite.Layer Setup()
        {
            h.NewProject(16);
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(1, 1);
            h.Exec("Sprite.Animation.AddFrame");
            h.SetColor(SKColors.Lime);
            h.DrawPixel(2, 2);
            h.Exec("Sprite.Animation.AddFrame");
            h.SetColor(SKColors.Blue);
            h.DrawPixel(3, 3);
            h.SetFrameIndex(0);
            return h.ActiveSprite.SelectedLayer!;
        }

        // Reads a pixel straight out of a frame's own bitmap, bypassing the "current frame" indirection.
        static SKColor FramePixel(Pix2dSprite.Layer layer, int frameIndex, int x, int y) =>
            layer.GetSpriteByFrame(frameIndex)?.Bitmap?.GetPixel(x, y) ?? SKColors.Transparent;

        t.Check("linked cels: nothing is linked by default", () =>
        {
            var layer = Setup();
            for (var i = 0; i < layer.FrameCount; i++)
                Assert.True(!layer.IsFrameLinked(i), $"frame {i} should not be linked on a fresh sprite");
            Assert.True(!h.SpriteEditor.IsCurrentFrameLinked, "the editor should report no link");
        });

        t.Check("linked cels: linking makes the frames share one image, keeping the source's pixels", () =>
        {
            var layer = Setup();
            var undoBefore = h.UndoStackSize;

            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            Assert.True(h.UndoStackSize == undoBefore + 1, "linking should be exactly one undo step");
            for (var i = 0; i < 3; i++)
                Assert.True(layer.IsFrameLinked(i), $"frame {i} should be linked");

            Assert.True(layer.GetLinkedFrameIndices(1).SequenceEqual(new[] { 0, 1, 2 }),
                $"the link group came out [{string.Join(", ", layer.GetLinkedFrameIndices(1))}]");

            // Frame 0's red pixel is now every frame's; frames 1 and 2 lost their own green/blue.
            for (var i = 0; i < 3; i++)
            {
                Assert.True(FramePixel(layer, i, 1, 1) == SKColors.Red, $"frame {i} should show the source's red pixel");
                Assert.True(FramePixel(layer, i, 2, 2).Alpha == 0, $"frame {i} should have lost frame 1's green pixel");
            }

            // One node for three frames is the memory win the feature is for.
            Assert.True(layer.Nodes.OfType<SpriteNode>().Count() == 1,
                $"3 linked frames should own 1 sprite node, got {layer.Nodes.OfType<SpriteNode>().Count()}");
        });

        // THE defining behaviour: an edit on one linked cel is an edit on all of them.
        t.Check("linked cels: drawing on one linked frame changes every frame in the link", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            h.SetFrameIndex(1);
            h.SetColor(SKColors.Yellow);
            h.DrawPixel(7, 7);

            for (var i = 0; i < 3; i++)
                Assert.True(FramePixel(layer, i, 7, 7) == SKColors.Yellow,
                    $"frame {i} should carry the stroke drawn on frame 1, got {FramePixel(layer, i, 7, 7)} " +
                    "(copy-on-write still splitting a linked cel?)");
        });

        // The compatibility guarantee: an UNLINKED shared node (what a plain duplicate produces, and what
        // older project files contain) must still copy-on-write, or editing an old file would corrupt it.
        t.Check("linked cels: a plain duplicate is still copy-on-write, not a link", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.Exec("Sprite.Animation.DuplicateFrame");

            var dup = h.SpriteEditor.CurrentFrameIndex;
            Assert.True(!layer.IsFrameLinked(dup), "a duplicate must not be a link");
            Assert.True(!layer.IsFrameLinked(0), "the duplicate's source must not become a link either");

            h.SetColor(SKColors.Yellow);
            h.DrawPixel(9, 9);

            Assert.True(FramePixel(layer, dup, 9, 9) == SKColors.Yellow, "the duplicate should carry the new stroke");
            Assert.True(FramePixel(layer, 0, 9, 9).Alpha == 0,
                "editing a duplicate must NOT change its source — that is the copy-on-write behaviour older " +
                "project files depend on");
        });

        // A duplicate of a linked frame is handed the source's node by the insert, and because that node has
        // copy-on-write disabled the duplicate would quietly follow the link until it is itself edited. So the
        // check that bites is editing the LINKED ORIGINAL and asserting the duplicate does not move — editing
        // the duplicate first would be split off by copy-on-write anyway and prove nothing.
        t.Check("linked cels: duplicating a LINKED frame yields a frame the link cannot reach", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1]);

            h.SetFrameIndex(0);
            h.Exec("Sprite.Animation.DuplicateFrame");
            var dup = h.SpriteEditor.CurrentFrameIndex;

            Assert.True(!layer.IsFrameLinked(dup), "the duplicate of a linked frame must not join the link");

            var linked = layer.GetLinkedFrameIndices(0).ToArray();
            Assert.True(linked.Length == 2 && !linked.Contains(dup),
                $"the link should still have exactly its two members and not the duplicate, got [{string.Join(", ", linked)}]");

            // Draw on a member of the link, not on the duplicate.
            h.SetFrameIndex(linked[0]);
            h.SetColor(SKColors.Yellow);
            h.DrawPixel(8, 8);

            foreach (var i in linked)
                Assert.True(FramePixel(layer, i, 8, 8) == SKColors.Yellow, $"linked frame {i} should take the stroke");

            Assert.True(FramePixel(layer, dup, 8, 8).Alpha == 0,
                $"the duplicate at index {dup} must not follow the link it was copied out of — " +
                $"got {FramePixel(layer, dup, 8, 8)}");
        });

        t.Check("linked cels: unlinking one frame gives it a private copy and leaves the rest linked", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            h.SetFrameIndex(1);
            h.SpriteEditor.UnlinkCurrentFrame();

            Assert.True(!layer.IsFrameLinked(1), "frame 1 should no longer be linked");
            Assert.True(layer.IsFrameLinked(0) && layer.IsFrameLinked(2), "frames 0 and 2 should stay linked");
            Assert.True(layer.GetLinkedFrameIndices(0).SequenceEqual(new[] { 0, 2 }),
                $"the remaining group came out [{string.Join(", ", layer.GetLinkedFrameIndices(0))}]");

            // It keeps the pixels it had, but now privately.
            Assert.True(FramePixel(layer, 1, 1, 1) == SKColors.Red, "the unlinked frame keeps the shared image");

            h.SetColor(SKColors.Yellow);
            h.DrawPixel(6, 6);
            Assert.True(FramePixel(layer, 1, 6, 6) == SKColors.Yellow, "the unlinked frame takes the stroke");
            Assert.True(FramePixel(layer, 0, 6, 6).Alpha == 0 && FramePixel(layer, 2, 6, 6).Alpha == 0,
                "the still-linked frames must not see it");
        });

        // A one-member "link" would keep drawing the marker and keep blocking copy-on-write for nothing.
        t.Check("linked cels: unlinking down to a single member drops the link entirely", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1]);

            h.SetFrameIndex(0);
            h.SpriteEditor.UnlinkCurrentFrame();

            Assert.True(!layer.IsFrameLinked(0) && !layer.IsFrameLinked(1),
                "with one member left there is no link, so neither frame should report one");
        });

        t.Check("linked cels: undo restores every frame's own pixels, and redo re-links", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            h.Exec("Edit.Undo");

            for (var i = 0; i < 3; i++)
                Assert.True(!layer.IsFrameLinked(i), $"frame {i} should not be linked after undo");

            Assert.True(FramePixel(layer, 0, 1, 1) == SKColors.Red, "frame 0 should be red again");
            Assert.True(FramePixel(layer, 1, 2, 2) == SKColors.Lime,
                $"frame 1's own green pixel must come back — got {FramePixel(layer, 1, 2, 2)} " +
                "(a link discards the followers' bitmaps, so undo has to restore the nodes, not recompute them)");
            Assert.True(FramePixel(layer, 2, 3, 3) == SKColors.Blue, "frame 2 should be blue again");
            Assert.True(layer.Nodes.OfType<SpriteNode>().Count() == 3,
                $"all three sprite nodes should be back, got {layer.Nodes.OfType<SpriteNode>().Count()}");

            h.Exec("Edit.Redo");
            for (var i = 0; i < 3; i++)
                Assert.True(layer.IsFrameLinked(i), $"frame {i} should be linked again after redo");
            Assert.True(FramePixel(layer, 1, 2, 2).Alpha == 0, "redo should drop frame 1's own pixels again");
        });

        t.Check("linked cels: undo of an unlink returns the frame to the link", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);
            h.SetFrameIndex(1);
            h.SpriteEditor.UnlinkCurrentFrame();

            h.Exec("Edit.Undo");

            Assert.True(layer.IsFrameLinked(1), "frame 1 should be linked again");
            Assert.True(layer.GetLinkedFrameIndices(1).SequenceEqual(new[] { 0, 1, 2 }),
                $"the group came out [{string.Join(", ", layer.GetLinkedFrameIndices(1))}]");
            Assert.True(layer.Nodes.OfType<SpriteNode>().Count() == 1,
                $"the unlink's private copy should be gone, got {layer.Nodes.OfType<SpriteNode>().Count()} nodes");
        });

        t.Check("linked cels: a no-op link or unlink pushes no undo step", () =>
        {
            var layer = Setup();
            var before = h.UndoStackSize;

            h.SetFrameIndex(0);
            h.SpriteEditor.UnlinkCurrentFrame();          // nothing is linked
            h.SpriteEditor.LinkFrames([0]);               // a single frame cannot be a link
            h.SpriteEditor.LinkFrames([]);                // nor can none

            Assert.True(h.UndoStackSize == before,
                $"undo stack grew from {before} to {h.UndoStackSize} on operations that change nothing");
            Assert.True(!layer.IsFrameLinked(0), "frame 0 should still not be linked");
        });

        // Clearing a linked cel used to BRICK it: ClearFrame detached the node while leaving IsLinked set, and
        // EnsureFrameHasUniqueSprite early-returns on IsLinked, so no sprite was ever rebuilt — later strokes
        // silently no-opped and the clear could not even be undone. Clearing must write through the link.
        t.Check("linked cels: clearing a linked frame clears the whole link and stays undoable", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            h.SetFrameIndex(1);
            h.Exec("Sprite.Edit.Clear");

            for (var i = 0; i < 3; i++)
                Assert.True(FramePixel(layer, i, 1, 1).Alpha == 0,
                    $"frame {i} should have been cleared through the link, got {FramePixel(layer, i, 1, 1)}");
            for (var i = 0; i < 3; i++)
                Assert.True(layer.IsFrameLinked(i), $"frame {i} should still be linked after a clear");

            // The frame must still accept paint — the bricked state swallowed strokes silently.
            h.SetColor(SKColors.Yellow);
            h.DrawPixel(5, 5);
            for (var i = 0; i < 3; i++)
                Assert.True(FramePixel(layer, i, 5, 5) == SKColors.Yellow,
                    $"frame {i} should take a stroke after the clear, got {FramePixel(layer, i, 5, 5)} " +
                    "(a cleared linked frame that can no longer be drawn on is the bricked state)");

            h.Exec("Edit.Undo");   // the stroke
            h.Exec("Edit.Undo");   // the clear
            Assert.True(FramePixel(layer, 0, 1, 1) == SKColors.Red,
                $"undoing the clear must bring the shared pixels back, got {FramePixel(layer, 0, 1, 1)}");
        });

        // Deleting one member of a two-frame link leaves a single frame that shares with nobody; keeping the
        // flag would draw a link marker that lies and block copy-on-write for nothing.
        t.Check("linked cels: deleting down to one member drops the link", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1]);

            h.SetFrameIndex(1);
            h.Exec("Sprite.Animation.DeleteFrame");

            Assert.True(!layer.IsFrameLinked(0),
                "the surviving frame shares with nobody, so it must not still report a link");
        });

        // Undo of a frame delete restored from a SINGLE node id shared by every layer, so with two layers
        // holding shared (linked) frames one layer's id overwrote the other's and that layer's frame came
        // back blank. It also rebuilt the meta from a bare id, dropping IsLinked.
        t.Check("linked cels: undoing a frame delete restores every layer's pixels and its link", () =>
        {
            var layer0 = Setup();
            h.Exec("Sprite.Edit.AddLayer");
            var layer1 = h.ActiveSprite.SelectedLayer!;
            Assert.True(!ReferenceEquals(layer0, layer1), "a second layer should have been added");

            // Give the new layer its own pixels, then link both layers so every frame takes the shared path.
            h.SetFrameIndex(0);
            h.SetColor(SKColors.Cyan);
            h.DrawPixel(4, 4);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            h.ActiveSprite.SelectLayer(layer0);
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 1, 2]);

            h.SetFrameIndex(1);
            h.Exec("Sprite.Animation.DeleteFrame");
            h.Exec("Edit.Undo");

            Assert.True(layer0.FrameCount == 3 && layer1.FrameCount == 3,
                $"both layers should be back to 3 frames, got {layer0.FrameCount} and {layer1.FrameCount}");

            Assert.True(FramePixel(layer0, 1, 1, 1) == SKColors.Red,
                $"layer 0's restored frame lost its pixels, got {FramePixel(layer0, 1, 1, 1)}");
            Assert.True(FramePixel(layer1, 1, 4, 4) == SKColors.Cyan,
                $"layer 1's restored frame lost its pixels, got {FramePixel(layer1, 1, 4, 4)} " +
                "(one layer's node id overwriting another's on undo?)");

            Assert.True(layer0.IsFrameLinked(1) && layer1.IsFrameLinked(1),
                "the restored frame must come back INSIDE its link — a frame sharing the group's pixels but " +
                "not flagged linked is the mixed state the invariant forbids");
        });

        // Re-running the command over an already identical group would push an undo step that restores the
        // state it started from — a "lost click" on Ctrl+Z.
        t.Check("linked cels: re-linking an already linked layer pushes no undo step", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.Exec("Sprite.Animation.LinkAllFrames");

            var after = h.UndoStackSize;
            h.Exec("Sprite.Animation.LinkAllFrames");

            Assert.True(h.UndoStackSize == after,
                $"undo stack grew from {after} to {h.UndoStackSize} on a re-link that changes nothing");
            for (var i = 0; i < layer.FrameCount; i++)
                Assert.True(layer.IsFrameLinked(i), $"frame {i} should still be linked");
        });

        t.Check("linked cels: the command list exposes link/unlink in the Sprite context", () =>
        {
            var names = h.Commands.GetCommands().Select(c => c.Name).ToArray();
            foreach (var name in new[] { "Sprite.Animation.LinkAllFrames", "Sprite.Animation.UnlinkFrame" })
                Assert.True(names.Contains(name), $"{name} is not registered");
        });

        t.Check("linked cels: LinkAllFrames links the whole layer through the command", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.Exec("Sprite.Animation.LinkAllFrames");

            for (var i = 0; i < layer.FrameCount; i++)
                Assert.True(layer.IsFrameLinked(i), $"frame {i} should be linked");
            Assert.True(h.SpriteEditor.IsCurrentFrameLinked, "the editor should report the current frame linked");

            h.Exec("Sprite.Animation.UnlinkFrame");
            Assert.True(!h.SpriteEditor.IsCurrentFrameLinked, "the command should have unlinked the current frame");
        });

        // Linking is per LAYER: a static background sharing one image while the layer above keeps animating
        // is the whole use case, so it must not touch the sibling layer.
        t.Check("linked cels: linking one layer leaves the other layers alone", () =>
        {
            Setup();
            h.Exec("Sprite.Edit.AddLayer");

            var layers = h.ActiveSprite.Layers.ToArray();
            Assert.True(layers.Length == 2, $"expected 2 layers, got {layers.Length}");

            h.SetFrameIndex(0);
            h.Exec("Sprite.Animation.LinkAllFrames");

            var selected = h.ActiveSprite.SelectedLayer!;
            var other = layers.First(l => !ReferenceEquals(l, selected));

            for (var i = 0; i < selected.FrameCount; i++)
                Assert.True(selected.IsFrameLinked(i), $"selected layer frame {i} should be linked");
            for (var i = 0; i < other.FrameCount; i++)
                Assert.True(!other.IsFrameLinked(i), $"the other layer's frame {i} must not be linked");
        });

        t.Check("linked cels: a link survives a save/load round-trip", () =>
        {
            var layer = Setup();
            h.SetFrameIndex(0);
            h.SpriteEditor.LinkFrames([0, 2]);
            Assert.True(layer.GetLinkedFrameIndices(0).SequenceEqual(new[] { 0, 2 }), "precondition: 0 and 2 linked");

            // The real save path: NodeSerializer writes the tree plus its bitmap data entries, and
            // ProjectFormat.DeserializeScene is the single load path the app and the autosave store share.
            using var serializer = new NodeSerializer();
            var json = serializer.Serialize(h.AppState.CurrentProject.SceneNode!);
            var reloaded = ProjectFormat.DeserializeScene(json, ProjectFormat.CurrentVersion,
                serializer.GetDataEntries());

            var reloadedLayer = reloaded.Nodes.OfType<Pix2dSprite>().First().Layers.First();
            Assert.True(reloadedLayer.IsFrameLinked(0) && reloadedLayer.IsFrameLinked(2),
                "the link flag must be persisted (LayerFrameMeta.ln)");
            Assert.True(!reloadedLayer.IsFrameLinked(1), "frame 1 was never linked");
            Assert.True(reloadedLayer.GetLinkedFrameIndices(0).SequenceEqual(new[] { 0, 2 }),
                $"the reloaded group came out [{string.Join(", ", reloadedLayer.GetLinkedFrameIndices(0))}]");
            Assert.True(reloadedLayer.GetSpriteByFrame(0)?.Id == reloadedLayer.GetSpriteByFrame(2)?.Id,
                "the reloaded frames must resolve to the SAME sprite node, or the link is cosmetic only");
        });
    }

    // --- Scenario 7: artboards (multiple sprites in one scene) --------------------------------------
    static void ArtboardScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Artboard scenario ===");
        var start = h.ArtboardCount;
        t.Check("AddArtboard adds one sprite to the scene", () =>
        {
            h.Exec("Sprite.Edit.AddArtboard");
            Assert.True(h.ArtboardCount == start + 1, $"artboards {start} -> {h.ArtboardCount}, expected +1");
        });
    }

    /// <summary>
    /// Runs an async export off the UI thread and waits with a deadline. `SetupWithoutStarting` installs a
    /// Dispatcher SynchronizationContext on the main thread, so blocking it on a task whose continuations
    /// post back to the dispatcher deadlocks; `Task.Run` starts without that context. The deadline turns a
    /// future hang into a failed check instead of a wedged harness.
    /// </summary>
    static void RunExport(Func<Task> action)
    {
        var task = Task.Run(action);
        Assert.True(task.Wait(TimeSpan.FromSeconds(60)), "export did not finish within 60s");
    }

    // --- Scenario 7a: batch export — scope resolution, artboard naming, real files on disk -----------
    // The Export dialog's destination rule lives in ExportService: one artboard through a single-file
    // exporter gets a Save dialog seeded with the artboard's name, everything else gets ONE folder picker
    // and every item is written into it. HeadlessFileService answers both pickers from a temp folder, so
    // this drives the real ExportItemsAsync — including the names that reach the filesystem.
    static void BatchExportScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Batch export scenario ===");

        h.NewProject(16);
        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Magenta);
        h.DrawPixel(2, 2);

        var exportService = h.Services.GetRequiredService<IExportService>();
        var files = (HeadlessFileService)h.Services.GetRequiredService<IFileService>();

        // Three artboards with deliberately awkward names: a plain one, one with characters no filesystem
        // accepts, and one whose name is only whitespace (must fall back rather than produce ".png").
        var first = h.Artboards[0];
        first.Name = "hero";
        h.Exec("Sprite.Edit.AddArtboard");
        var second = h.Artboards[1];
        second.Name = "boss: phase 2/final";
        h.Exec("Sprite.Edit.AddArtboard");
        var third = h.Artboards[2];
        third.Name = "   ";

        t.Check("ExportFileNames.Sanitize strips invalid characters and collapses whitespace", () =>
        {
            Assert.True(ExportFileNames.Sanitize("boss: phase 2/final") == "boss phase 2final",
                $"got '{ExportFileNames.Sanitize("boss: phase 2/final")}'");
            Assert.True(ExportFileNames.Sanitize("a\\b*c?") == "abc", $"got '{ExportFileNames.Sanitize("a\\b*c?")}'");
            Assert.True(ExportFileNames.Sanitize("trailing dot.") == "trailing dot", "trailing dot must be trimmed");
            Assert.True(ExportFileNames.Sanitize("   ") == "", "whitespace-only must sanitize to empty");
            Assert.True(ExportFileNames.SanitizeOrFallback(null) == ExportFileNames.Fallback,
                "null must fall back");
        });

        t.Check("AllSprites scope yields one item per artboard, in scene order, named after it", () =>
        {
            var items = exportService.GetExportItems(ExportScope.AllSprites);
            Assert.True(items.Count == 3, $"expected 3 items, got {items.Count}");
            Assert.True(items[0].Name == "hero", $"item 0 named '{items[0].Name}'");
            Assert.True(items[1].Name == "boss phase 2final", $"item 1 named '{items[1].Name}'");
            Assert.True(items[2].Name == ExportFileNames.Fallback,
                $"blank artboard name must fall back to '{ExportFileNames.Fallback}', got '{items[2].Name}'");
        });

        t.Check("SelectedSprites scope follows the node selection, in scene order", () =>
        {
            h.SelectNodes(third, first); // click order deliberately reversed
            var items = exportService.GetExportItems(ExportScope.SelectedSprites);
            Assert.True(items.Count == 2, $"expected 2 items, got {items.Count}");
            Assert.True(items[0].Name == "hero" && items[1].Name == ExportFileNames.Fallback,
                $"expected scene order [hero, {ExportFileNames.Fallback}], got [{items[0].Name}, {items[1].Name}]");
        });

        t.Check("SelectedSprites falls back to the edited artboard when nothing is node-selected", () =>
        {
            h.SelectNodes();
            var items = exportService.GetExportItems(ExportScope.SelectedSprites);
            Assert.True(items.Count == 1, $"expected 1 item, got {items.Count}");
        });

        t.Check("single-artboard export suggests the artboard name, not 'untitled'", () =>
        {
            var single = exportService.GetExportItems(ExportScope.AllSprites).Take(1).ToList();
            var before = files.FolderPickerCalls;
            RunExport(() => exportService.ExportItemsAsync(single, 1, new PngImageExporter(files)));

            Assert.True(files.LastSuggestedFileName == "hero",
                $"expected suggested name 'hero', got '{files.LastSuggestedFileName ?? "<null>"}'");
            Assert.True(files.FolderPickerCalls == before,
                "a single-file export must use the Save dialog, not the folder picker");
            Assert.True(File.Exists(Path.Combine(files.RootPath, "hero.png")), "hero.png was not written");
        });

        t.Check("batch PNG export asks for one folder and writes one file per artboard", () =>
        {
            var items = exportService.GetExportItems(ExportScope.AllSprites);
            var before = files.FolderPickerCalls;
            h.Dialogs.YesNoAnswer = true; // the folder already holds hero.png from the previous check
            RunExport(() => exportService.ExportItemsAsync(items, 1, new PngImageExporter(files)));

            Assert.True(files.FolderPickerCalls == before + 1,
                $"expected exactly one folder prompt, got {files.FolderPickerCalls - before}");
            foreach (var item in items)
            {
                var path = Path.Combine(files.RootPath, item.Name + ".png");
                Assert.True(File.Exists(path), $"missing {item.Name}.png");
                Assert.True(new FileInfo(path).Length > 0, $"{item.Name}.png is empty");
            }
        });

        t.Check("declining the overwrite prompt leaves existing files untouched", () =>
        {
            // Replace a real export with a sentinel: if the declined export still ran, it would be gone.
            var heroPath = Path.Combine(files.RootPath, "hero.png");
            const string sentinel = "not-a-png";
            File.WriteAllText(heroPath, sentinel);

            h.Dialogs.YesNoAnswer = false;
            var items = exportService.GetExportItems(ExportScope.AllSprites);
            RunExport(() => exportService.ExportItemsAsync(items, 1, new PngImageExporter(files)));

            Assert.True(File.ReadAllText(heroPath) == sentinel,
                "the declined export must not overwrite anything");
        });

        t.Check("batch sheet export writes a PNG + JSON sidecar per artboard, both name-derived", () =>
        {
            var sheetDir = Path.Combine(files.RootPath, "sheets");
            var folder = new Pix2d.Common.FileSystem.NetFolder(sheetDir);
            var exporter = new SpriteSheetExporter(files, h.Services.GetRequiredService<IPlatformStuffService>());

            Assert.True(!exporter.NeedsOwnFolderPerItem, "a sheet's files are name-derived — no subfolder needed");

            foreach (var item in exportService.GetExportItems(ExportScope.AllSprites))
            {
                var it = item;
                RunExport(() => exporter.ExportToFolderAsync(it.Nodes, 1, folder, it.Name));
            }

            foreach (var item in exportService.GetExportItems(ExportScope.AllSprites))
            {
                Assert.True(File.Exists(Path.Combine(sheetDir, item.Name + ".png")), $"missing sheet {item.Name}.png");
                var json = Path.Combine(sheetDir, item.Name + ".json");
                Assert.True(File.Exists(json), $"missing sidecar {item.Name}.json");
                var meta = JObject.Parse(File.ReadAllText(json));
                Assert.True((string?)meta["meta"]?["image"] == item.Name + ".png",
                    $"sidecar meta.image should name the sheet next to it, got '{(string?)meta["meta"]?["image"]}'");
            }
        });

        // The Unity preset is the only emitter whose FileExtension is a DOUBLE extension (".png.meta", because
        // Unity's sidecar keeps the asset's own name). Both write paths compose the name differently, so the
        // batch one gets asserted against real files here rather than reasoned about.
        t.Check("batch sheet export composes the Unity double extension as <name>.png.meta", () =>
        {
            var unityDir = Path.Combine(files.RootPath, "unity");
            var folder = new Pix2d.Common.FileSystem.NetFolder(unityDir);
            var exporter = new SpriteSheetExporter(files, h.Services.GetRequiredService<IPlatformStuffService>())
            {
                MetadataFormat = "unity"
            };

            foreach (var item in exportService.GetExportItems(ExportScope.AllSprites))
            {
                var it = item;
                RunExport(() => exporter.ExportToFolderAsync(it.Nodes, 1, folder, it.Name));
            }

            foreach (var item in exportService.GetExportItems(ExportScope.AllSprites))
            {
                var metaPath = Path.Combine(unityDir, item.Name + ".png.meta");
                Assert.True(File.Exists(metaPath),
                    $"expected {item.Name}.png.meta; folder holds [{string.Join(", ", Directory.GetFiles(unityDir).Select(Path.GetFileName))}]");
                Assert.True(!File.Exists(Path.Combine(unityDir, item.Name + ".meta")),
                    "a single-extension .meta would not be picked up by Unity's asset database");
                Assert.True(File.ReadAllText(metaPath).StartsWith("fileFormatVersion: 2", StringComparison.Ordinal),
                    "the sidecar content should be the Unity meta, not the JSON default");
            }
        });

        t.Check("batch PNG-sequence export isolates each artboard in its own subfolder", () =>
        {
            var seqRoot = Path.Combine(files.RootPath, "sequence");
            var folder = new Pix2d.Common.FileSystem.NetFolder(seqRoot);
            var exporter = new SpritePngSequenceExporter();

            Assert.True(exporter.NeedsOwnFolderPerItem,
                "a sequence names its own frame files — the service must give it a folder per artboard");

            foreach (var item in exportService.GetExportItems(ExportScope.AllSprites))
            {
                var target = folder.GetSubfolder(item.Name);
                var it = item;
                RunExport(() => exporter.ExportToFolderAsync(it.Nodes, 1, target, it.Name));
            }

            foreach (var item in exportService.GetExportItems(ExportScope.AllSprites))
            {
                var frame = Path.Combine(seqRoot, item.Name, "frame_0000.png");
                Assert.True(File.Exists(frame), $"missing {item.Name}/frame_0000.png");
            }
        });

        t.Check("a cancelled picker neither throws nor reports success", () =>
        {
            files.PickerSucceeds = false;
            try
            {
                var items = exportService.GetExportItems(ExportScope.AllSprites);
                RunExport(() => exportService.ExportItemsAsync(items, 1, new PngImageExporter(files)));
            }
            finally
            {
                files.PickerSucceeds = true;
            }
        });
    }

    // --- Scenario 7ab: degenerate (0x0) canvases can neither be created nor drawn on -----------------
    // A sprite that reaches the editor at 0x0 made *every* stroke attempt throw "Bitmap is null"
    // (appstat 3.10.0, `app_context: canvas=0x0`) — the user faced an editor they could not draw in at
    // all. The defence is layered: CanvasSize clamps at creation and at every canvas mutation,
    // SceneIntegrity repairs a document that already carries one, and DrawingLayerNode.BeginDrawing
    // refuses to open a drawing operation on a degenerate target instead of throwing mid-stroke.
    static void DegenerateCanvasScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Degenerate canvas scenario ===");

        t.Check("CreateEmpty clamps a 0x0 request to a drawable canvas", () =>
        {
            var sprite = Pix2dSprite.CreateEmpty(SKSize.Empty);
            Assert.True(!CanvasSize.IsDegenerate(sprite.Size), $"sprite size is {sprite.Size}");
            Assert.True(!CanvasSize.IsDegenerate(sprite.Layers.First().Size),
                $"layer size is {sprite.Layers.First().Size}");
        });

        t.Check("a sub-pixel crop leaves a drawable canvas", () =>
        {
            // Frame bitmaps materialise lazily, so go through CreateFromBitmap to get real pixels to crop.
            var sprite = Pix2dSprite.CreateFromBitmap(new SKBitmap(16, 16));

            // Crop bounds come from a selection or a resize drag and can be sub-pixel; ToSizeI() then
            // truncates to 0. The interactive path guards this, the artboard Resize/Crop sub-mode and
            // undo replay do not — so the model layer has to.
            sprite.Crop(SKRect.Create(0, 0, 0.4f, 0.4f));

            Assert.True(!CanvasSize.IsDegenerate(sprite.Size), $"sprite size is {sprite.Size}");
            var frame = sprite.Layers.First().Nodes.OfType<SpriteNode>().First();
            Assert.True(frame.Bitmap is { Width: > 0, Height: > 0 }, "crop produced a zero-sized bitmap");
        });

        t.Check("SceneIntegrity recovers a lost canvas size from the frame pixels", () =>
        {
            var scene = new SKNode { Name = "Scene" };
            var sprite = Pix2dSprite.CreateFromBitmap(new SKBitmap(24, 12));
            scene.Nodes.Add(sprite);

            // What a damaged/partially-restored document looks like: pixels intact, container size gone.
            sprite.Size = SKSize.Empty;
            sprite.Layers.First().Size = SKSize.Empty;

            SceneIntegrity.Repair(scene);

            Assert.True(sprite.Size == new SKSize(24, 12),
                $"expected the size to be recovered as 24x12, got {sprite.Size}");
            Assert.True(!CanvasSize.IsDegenerate(sprite.Layers.First().Size), "the layer size was not repaired");
        });

        t.Check("SceneIntegrity falls back to the layer size when no pixels survive", () =>
        {
            var scene = new SKNode { Name = "Scene" };
            var sprite = Pix2dSprite.CreateEmpty(new SKSize(48, 20));   // empty frames — no bitmaps yet
            scene.Nodes.Add(sprite);
            sprite.Size = SKSize.Empty;

            SceneIntegrity.Repair(scene);

            Assert.True(sprite.Size == new SKSize(48, 20),
                $"expected the size to be recovered as 48x20, got {sprite.Size}");
        });

        t.Check("a sprite with no recoverable pixels falls back to a usable canvas", () =>
        {
            var scene = new SKNode { Name = "Scene" };
            var sprite = new Pix2dSprite { Size = SKSize.Empty };
            scene.Nodes.Add(sprite);

            SceneIntegrity.Repair(scene);

            Assert.True(!CanvasSize.IsDegenerate(sprite.Size), $"sprite size is still {sprite.Size}");
        });

        // End-to-end: poison the *live* editing target the way the crash reports describe (canvas size
        // gone, frame pixels released) and drive a real pointer gesture through SKInput.
        h.NewProject();
        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Red);
        h.DrawPixel(2, 2);   // materialises the frame's SpriteNode — an empty frame has no bitmap at all

        var active = h.ActiveSprite;
        var activeFrame = active.Layers.First().Nodes.OfType<SpriteNode>().First();
        var originalSize = active.Size;

        activeFrame.Size = SKSize.Empty;
        activeFrame.Bitmap = null;   // pixels disposed on unload / image missing from a restored session
        active.Size = SKSize.Empty;

        t.Check("pointer-down on a 0x0 canvas does not throw", () =>
        {
            h.PressWorld(4, 4);
            h.MoveWorld(5, 5, pressed: true);
            h.ReleaseWorld(5, 5);
        });

        t.Check("the editor still draws once the canvas size is restored", () =>
        {
            // What SceneIntegrity does on load, applied live: restoring the size re-materialises the
            // frame's bitmap through BitmapNode.OnSizeChanged.
            activeFrame.Size = originalSize;
            active.Size = originalSize;

            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Lime);
            h.DrawPixel(3, 3);

            Assert.True(h.GetPixel(3, 3).Alpha > 0, "the pixel did not land after the canvas was restored");
        });

        // Leave a clean project behind — the poisoned frame above is not worth threading through the
        // scenarios that follow.
        h.NewProject();
    }

    // --- Scenario 7ad: an oversized canvas request cannot poison the document -----------------------
    // The mirror image of 7ab, and the top live signature on 3.11.2: "Unable to allocate pixels for the
    // bitmap." (21 events / 7 users), one session stuck at `app_context: canvas=64344556x64` — a typo in
    // the (then unbounded) canvas-size panel. Because Pix2dSprite.Crop assigns Size *before* resizing its
    // layers, the failed allocation left the sprite holding an impossible size, and every later operation
    // that re-derives a bitmap from it (DrawingLayerNode.SetTarget allocates three) threw again. The
    // defence is the same shape as the min-size one: CanvasSize clamps both ends at every choke point.
    static void OversizedCanvasScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Oversized canvas scenario ===");

        t.Check("CanvasSize clamps an absurd dimension to the allocatable maximum", () =>
        {
            var clamped = CanvasSize.Sanitize(new SKSize(64344556, 64));
            Assert.True(clamped.Width == CanvasSize.MaxDimension, $"width is {clamped.Width}");
            Assert.True(clamped.Height == 64, $"height {clamped.Height} should have passed through");
            Assert.True(!CanvasSize.IsOversized(clamped), "the clamped size is still oversized");
        });

        t.Check("a canvas at the limit cannot overflow a 32-bit pixel-buffer size", () =>
        {
            // The reason MaxDimension is 16384 and not something rounder.
            var max = (long)CanvasSize.MaxDimension;
            Assert.True(max * max * 4 <= (long)int.MaxValue + 1,
                $"{max}x{max}x4 = {max * max * 4} exceeds the addressable buffer size");
        });

        t.Check("a crop to an unallocatable size leaves the sprite usable", () =>
        {
            var sprite = Pix2dSprite.CreateFromBitmap(new SKBitmap(16, 16));

            sprite.Crop(SKRect.Create(0, 0, 64344556, 64));

            Assert.True(!CanvasSize.IsOversized(sprite.Size), $"sprite size is {sprite.Size}");
            var frame = sprite.Layers.First().Nodes.OfType<SpriteNode>().First();
            Assert.True(frame.Bitmap is { Width: > 0, Height: > 0 }, "the crop left the frame without pixels");
        });

        // End to end through the real command + operation path: the panel's Apply on a nonsense width.
        h.NewProject();
        var editService = h.Services.GetRequiredService<IEditService>();

        t.Check("Apply on a nonsense canvas width does not throw", () =>
            editService.CropCurrentSprite(new SKSize(64344556, 64), 0.5f, 0.5f));

        t.Check("and the editor keeps drawing afterwards", () =>
        {
            // This is what used to fail forever after: the next stroke re-enters DrawingLayerNode.SetTarget,
            // which allocates working/background/swap bitmaps from the (previously impossible) target size.
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(2, 2);

            Assert.True(h.GetPixel(2, 2).Alpha > 0, "the stroke did not land after an oversized crop");
        });

        h.NewProject();
    }

    // --- Scenario 7ae: a selection rebuilt mid-drag must not kill the drag -------------------------
    // MoveThumbNode snapshots the dragged nodes' positions once, on DragStarted, then indexed that
    // snapshot on every DragDelta. The selection is not frozen for the duration of a gesture though — the
    // pointer stays captured by the thumb while other code re-selects — so a node that joined the
    // selection mid-gesture had no snapshot entry and the next pointer-move threw KeyNotFoundException:
    // appstat 3.11.3, `'Pix2d.Plugins.Drawing.Nodes.SpriteSelectionNode' was not present in the
    // dictionary`, out of `tool=PixelTransformTool` (its lift materialises that node after the press).
    // Reproduced here with artboards in General context because the invariant belongs to the thumb, not
    // to any one tool: whatever changes the selection, the drag must keep working.
    static void MoveThumbSelectionChurnScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Move-thumb selection churn scenario ===");

        h.NewProject(64);
        h.Exec("Sprite.Edit.AddArtboard");

        var sprites = h.AppState.CurrentProject.SceneNode!.Nodes.OfType<Pix2dSprite>().ToArray();
        var a = sprites[0];
        var b = sprites[1];
        var aBox = a.GetBoundingBox();

        h.AppState.CurrentProject.CurrentContextType = EditContextType.General;
        h.SetView(1);   // 1:1, so the screen-pixel-sized thumb hit zones stay where the maths says

        t.Check("a node that joins the selection mid-drag does not break the gesture", () =>
        {
            // Press starts a select-and-drag on `a` alone: the thumb captures the pointer and snapshots {a}.
            h.PressWorld(aBox.MidX, aBox.MidY);
            h.MoveWorld(aBox.MidX + 4, aBox.MidY, pressed: true);

            // Mid-gesture re-selection, pointer still captured by the thumb — {a} becomes {a, b}.
            h.SelectNodes(a, b);

            h.MoveWorld(aBox.MidX + 8, aBox.MidY, pressed: true);
            h.ReleaseWorld(aBox.MidX + 8, aBox.MidY);
        });

        t.Check("the newcomer is carried by the rest of the drag, not teleported by it", () =>
        {
            // Adopting a mid-drag node back-dates its origin by the delta already applied, so it must not
            // jump by the whole gesture — only by what happened after it joined.
            var moved = b.Position;

            h.SelectNodes(a, b);
            h.PressWorld(aBox.MidX, aBox.MidY);
            h.MoveWorld(aBox.MidX + 3, aBox.MidY, pressed: true);
            h.ReleaseWorld(aBox.MidX + 3, aBox.MidY);

            Assert.True(b.Position == new SKPoint(moved.X + 3, moved.Y),
                $"expected the second artboard at +3, got {moved} -> {b.Position}");
        });

        h.NewProject();
    }

    // --- Scenario 7af: overwriting a read-only file ------------------------------------------------
    // Windows marks files extracted from a .zip read-only. Export-over-existing and Ctrl+S on a
    // PNG-backed project both write through IFileContentSource, whose delete-then-write pair failed with
    // UnauthorizedAccessException on exactly such a file (appstat 3.11.3, a PNG opened from a Minecraft
    // resource-pack zip in Temp). Overwriting is the confirmed intent of both operations.
    static void ReadOnlyOverwriteScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Read-only overwrite scenario ===");

        var dir = Path.Combine(Path.GetTempPath(), "Pix2d.ScenarioTests", "readonly-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ladder.png");

        try
        {
            File.WriteAllText(path, "stale content that must be replaced entirely");
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            var file = new Pix2d.Common.FileSystem.NetFileSource(path);

            t.Check("SaveAsync overwrites a read-only file", () =>
            {
                using var source = new MemoryStream(new byte[] { 1, 2, 3 });
                file.SaveAsync(source).GetAwaiter().GetResult();

                var written = File.ReadAllBytes(path);
                Assert.True(written.Length == 3, $"expected the file to be truncated to 3 bytes, got {written.Length}");
            });

            t.Check("a staged write overwrites a read-only file", () =>
            {
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

                var staged = file.OpenStagedWriteAsync().GetAwaiter().GetResult();
                staged.Stream.Write([9, 9]);
                staged.CommitAsync().GetAwaiter().GetResult();
                staged.DisposeAsync().GetAwaiter().GetResult();

                Assert.True(File.ReadAllBytes(path).Length == 2, "the staged write did not replace the file");
            });

            // A save that dies mid-write used to leave the destination truncated, which for a .pix2d is an
            // unopenable project where the user's work was (appstat, fatal: "End of Central Directory
            // record could not be found", alongside disk-full errors from the same window). SaveAsync now
            // stages through a .tmp sibling and only moves it into place once the bytes are all there.
            t.Check("a failed SaveAsync leaves the previous file untouched", () =>
            {
                File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);

                using var source = new ThrowingStream(failAfterBytes: 2);
                var threw = false;
                try { file.SaveAsync(source).GetAwaiter().GetResult(); }
                catch (IOException) { threw = true; }

                Assert.True(threw, "the write failure must still be reported to the caller");
                Assert.True(File.ReadAllBytes(path).Length == 8,
                    "the destination was modified by a save that never completed");
                Assert.True(!File.Exists(path + ".tmp"), "the staging file was left behind");
            });

            // The staged-write path is what lets a multi-megabyte project stream straight to disk instead of
            // being assembled in memory first, so it has to give the same guarantee the buffer did: the
            // destination is untouched until the payload is complete. Commit publishes; abandoning does not.
            t.Check("a staged write publishes only on commit", () =>
            {
                File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);

                var staged = file.OpenStagedWriteAsync().GetAwaiter().GetResult();
                staged.Stream.Write([7, 7, 7]);
                Assert.True(File.ReadAllBytes(path).Length == 8,
                    "the destination changed before the commit");

                staged.CommitAsync().GetAwaiter().GetResult();
                staged.DisposeAsync().GetAwaiter().GetResult();

                Assert.True(File.ReadAllBytes(path).Length == 3, "the commit did not publish the staged bytes");
                Assert.True(!File.Exists(path + ".tmp"), "the staging file outlived the commit");
            });

            t.Check("an abandoned staged write leaves the previous file untouched", () =>
            {
                File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);

                var staged = file.OpenStagedWriteAsync().GetAwaiter().GetResult();
                staged.Stream.Write([9, 9]);
                staged.DisposeAsync().GetAwaiter().GetResult();   // no commit — the write failed

                Assert.True(File.ReadAllBytes(path).Length == 8, "an uncommitted write reached the destination");
                Assert.True(!File.Exists(path + ".tmp"), "the abandoned staging file was left behind");
            });

            // ProjectPacker now streams the archive into the staging file rather than assembling it in
            // memory, which puts the zip's central directory at the mercy of commit ordering: commit before
            // the ZipArchive is disposed and every saved project would be missing it. The format harness
            // round-trips through the serializer, not through the packer, so this is the check that the
            // production save path still produces a readable archive.
            t.Check("a project written through the staged path reloads", () =>
            {
                h.NewProject(32);
                h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
                h.DragWorld(4, 4, 20, 20);

                var scene = h.AppState.CurrentProject.SceneNode!;
                var expected = scene.Nodes.OfType<Pix2dSprite>().Count();

                var projectPath = Path.Combine(dir, "roundtrip.pix2d");
                var projectFile = new Pix2d.Common.FileSystem.NetFileSource(projectPath);

                // Task.Run: these checks run on the Avalonia dispatcher thread, and the packer's awaits post
                // their continuations back to it — blocking on them from that same thread would deadlock.
                // Production always awaits WriteProjectAsync properly (ProjectService.SaveCurrentProjectToFileAsync).
                Task.Run(() => Pix2d.Project.ProjectPacker.WriteProjectAsync(projectFile, scene))
                    .GetAwaiter().GetResult();

                Assert.True(!File.Exists(projectPath + ".tmp"), "the staging file outlived the save");

                var reloaded = Task.Run(() => Pix2d.Project.ProjectUnpacker.LoadProjectScene(projectFile))
                    .GetAwaiter().GetResult();

                Assert.True(reloaded != null, "the saved project did not load back");
                Assert.True(reloaded!.Nodes.OfType<Pix2dSprite>().Count() == expected,
                    "the reloaded project lost artboards");

                var preview = Task.Run(() => Pix2d.Project.ProjectUnpacker.LoadPreview(projectFile))
                    .GetAwaiter().GetResult();
                Assert.True(preview != null, "the saved project carries no readable thumbnail");
            });

            // The recent-projects gallery asks every entry for a thumbnail on a fire-and-forget task, so a
            // corrupt .pix2d among them took the whole app down (appstat, fatal: "End of Central Directory
            // record could not be found"). A thumbnail that cannot be read is a missing thumbnail.
            t.Check("a corrupt .pix2d yields no preview instead of throwing", () =>
            {
                var corruptPath = Path.Combine(dir, "corrupt.pix2d");
                File.WriteAllBytes(corruptPath, "PK truncated before the central directory"u8.ToArray());

                var preview = Pix2d.Project.ProjectUnpacker
                    .LoadPreview(new Pix2d.Common.FileSystem.NetFileSource(corruptPath))
                    .GetAwaiter().GetResult();

                Assert.True(preview == null, "a corrupt archive must not produce a preview");
            });
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.SetAttributes(path, FileAttributes.Normal);
                Directory.Delete(dir, true);
            }
            catch { /* temp cleanup is best effort */ }
        }
    }

    /// <summary>
    /// Readable stream that hands over a few bytes and then fails, standing in for a full disk or a drive
    /// pulled mid-save. Derives from <see cref="Stream"/>, not MemoryStream — the latter's optimized
    /// CopyToAsync bypasses an overridden Read and never sees the failure.
    /// </summary>
    private sealed class ThrowingStream(int failAfterBytes) : Stream
    {
        private int _delivered;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_delivered >= failAfterBytes)
                throw new IOException("There is not enough space on the disk.");

            var n = Math.Min(count, failAfterBytes - _delivered);
            for (var i = 0; i < n; i++) buffer[offset + i] = 0xAB;
            _delivered += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _delivered; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // --- Scenario 7ag: a logging target that throws ------------------------------------------------
    // Logger.Dispatch runs on the error path — Logger.LogException is what most catch blocks call. A
    // target that threw there replaced the error being reported with a fatal unhandled exception, and
    // starved every target after it, including the crash-telemetry sink. That is how a full disk turned
    // a failed export into a crash (appstat 3.11.3: IOException writing pix2d_log.txt).
    static void LoggerTargetFailureScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Logger target failure scenario ===");

        var exploding = new ThrowingLoggerTarget();
        var witness = new RecordingLoggerTarget();

        Logger.RegisterLoggerTarget(exploding);
        Logger.RegisterLoggerTarget(witness);
        try
        {
            t.Check("a throwing logger target does not escape Logger.LogException", () =>
            {
                Logger.LogException(new InvalidOperationException("export failed"));
                Assert.True(exploding.Calls == 1, $"the target was not reached, got {exploding.Calls} calls");
            });

            t.Check("targets after a throwing one still receive the entry", () =>
                Assert.True(witness.Calls == 1, $"expected 1 entry, got {witness.Calls}"));
        }
        finally
        {
            Logger.UnregisterLoggerTarget(exploding);
            Logger.UnregisterLoggerTarget(witness);
        }
    }

    private sealed class ThrowingLoggerTarget : ILoggerTarget
    {
        public int Calls;
        public bool EventsOnly => false;

        public void OnLogged(LogEntry logEntry)
        {
            Calls++;
            throw new IOException("There is not enough space on the disk.");
        }
    }

    private sealed class RecordingLoggerTarget : ILoggerTarget
    {
        public int Calls;
        public bool EventsOnly => false;

        public void OnLogged(LogEntry logEntry) => Calls++;
    }

    // --- Scenario 7ac: animation metadata — tags, per-frame durations, index shifting ---------------
    // The metadata is index-keyed over a timeline the user constantly mutates, so the risky part isn't
    // storing it, it's keeping it aligned while frames are inserted/deleted/reordered — the same class
    // of index bug that produced the timeline crash signatures on 3.11.1. Every rule below is asserted
    // through the *real* operations (via the command service) and each is re-asserted after undo AND
    // after redo, because redo re-runs OnPerform on state the undo just restored.
    static void AnimationMetaScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Animation metadata scenario ===");

        // A 5-frame sprite: frames 0..4.
        h.NewProject();
        for (var i = 0; i < 4; i++)
            h.Exec("Sprite.Animation.AddFrame");

        var sprite = h.ActiveSprite;
        t.Check("harness built a 5-frame sprite", () =>
            Assert.True(sprite.GetFramesCount() == 5, $"expected 5 frames, got {sprite.GetFramesCount()}"));

        t.Check("a frame duration override wins over the frame rate", () =>
        {
            sprite.FrameRate = 10;                       // default 100 ms
            sprite.SetFrameDurationMs(2, 250);

            Assert.True(sprite.GetFrameDurationMs(2) == 250, $"frame 2: {sprite.GetFrameDurationMs(2)} ms");
            Assert.True(sprite.GetFrameDurationMs(1) == 100, $"frame 1: {sprite.GetFrameDurationMs(1)} ms");
            Assert.True(sprite.HasFrameDurationOverride(2) && !sprite.HasFrameDurationOverride(1),
                "override flags disagree with the values");
        });

        t.Check("clearing the only override leaves no duration list at all", () =>
        {
            sprite.SetFrameDurationMs(2, null);
            Assert.True(sprite.FrameDurations == null,
                $"expected the list to be dropped, got [{string.Join(",", sprite.FrameDurations ?? [])}]");
            sprite.SetFrameDurationMs(2, 250);           // restore for the shift checks below
        });

        // "run" spans 1..3; the insert/delete rules are all expressed relative to it.
        sprite.AnimationTags = [new SpriteAnimationTag { Name = "run", From = 1, To = 3 }];

        t.Check("inserting inside a tag extends it and shifts the durations", () =>
        {
            h.SetFrameIndex(1);
            h.Exec("Sprite.Animation.AddFrame");         // inserts at 2

            var tag = sprite.AnimationTags!.Single();
            Assert.True(tag is { From: 1, To: 4 }, $"expected run=1..4, got {tag.From}..{tag.To}");
            Assert.True(sprite.GetFrameDurationMs(3) == 250,
                $"the 250 ms frame should have slid 2 -> 3, reads {sprite.GetFrameDurationMs(3)} ms at 3");
            Assert.True(!sprite.HasFrameDurationOverride(2), "the inserted frame should be default-timed");
        });

        t.Check("undo puts the tag range and the durations back", () =>
        {
            h.Exec("Edit.Undo");

            var tag = sprite.AnimationTags!.Single();
            Assert.True(tag is { From: 1, To: 3 }, $"expected run=1..3 after undo, got {tag.From}..{tag.To}");
            Assert.True(sprite.GetFrameDurationMs(2) == 250,
                $"the override should be back on frame 2, reads {sprite.GetFrameDurationMs(2)} ms");
        });

        t.Check("redo re-applies the same shift (no drift on the second run)", () =>
        {
            h.Exec("Edit.Redo");

            var tag = sprite.AnimationTags!.Single();
            Assert.True(tag is { From: 1, To: 4 }, $"expected run=1..4 after redo, got {tag.From}..{tag.To}");
            Assert.True(sprite.GetFrameDurationMs(3) == 250, "the duration did not follow its frame on redo");

            h.Exec("Edit.Undo");   // back to the 5-frame / run=1..3 baseline
        });

        t.Check("deleting a frame before a tag slides the whole range left", () =>
        {
            h.SetFrameIndex(0);
            h.Exec("Sprite.Animation.DeleteFrame");

            var tag = sprite.AnimationTags!.Single();
            Assert.True(tag is { From: 0, To: 2 }, $"expected run=0..2, got {tag.From}..{tag.To}");
            Assert.True(sprite.GetFrameDurationMs(1) == 250,
                $"the override should have slid 2 -> 1, reads {sprite.GetFrameDurationMs(1)} ms at 1");

            h.Exec("Edit.Undo");
        });

        t.Check("a tag whose only frame is deleted is dropped, and undo brings it back", () =>
        {
            sprite.AnimationTags!.Add(new SpriteAnimationTag { Name = "hit", From = 4, To = 4 });

            h.SetFrameIndex(4);
            h.Exec("Sprite.Animation.DeleteFrame");
            Assert.True(sprite.AnimationTags?.Any(x => x.Name == "hit") != true,
                "the single-frame tag should be gone with its frame");

            h.Exec("Edit.Undo");
            var restored = sprite.AnimationTags?.FirstOrDefault(x => x.Name == "hit");
            Assert.True(restored is { From: 4, To: 4 },
                "undo did not restore the dropped tag — this is exactly what the snapshot exists for");

            sprite.AnimationTags!.RemoveAll(x => x.Name == "hit");
        });

        t.Check("a single-frame tag follows its frame across a reorder", () =>
        {
            sprite.AnimationTags!.Add(new SpriteAnimationTag { Name = "hit", From = 4, To = 4 });

            h.ReorderFrames(4, 0);

            var hit = sprite.AnimationTags?.FirstOrDefault(x => x.Name == "hit");
            Assert.True(hit is { From: 0, To: 0 },
                $"expected hit=0..0 after the move, got {(hit == null ? "dropped" : $"{hit.From}..{hit.To}")}");

            h.Exec("Edit.Undo");
            hit = sprite.AnimationTags?.FirstOrDefault(x => x.Name == "hit");
            Assert.True(hit is { From: 4, To: 4 }, "undo did not restore the tag's original range");
            sprite.AnimationTags!.RemoveAll(x => x.Name == "hit");
        });

        t.Check("a duplicated frame inherits the source frame's duration", () =>
        {
            h.SetFrameIndex(2);                          // the 250 ms frame
            h.Exec("Sprite.Animation.DuplicateFrame");

            Assert.True(sprite.GetFrameDurationMs(3) == 250,
                $"the duplicate should also run 250 ms, reads {sprite.GetFrameDurationMs(3)} ms");

            h.Exec("Edit.Undo");
        });

        t.Check("editing metadata through the editor is one undoable step", () =>
        {
            var before = h.UndoStackSize;
            var tag = h.SpriteEditor.AddAnimationTag("idle");

            Assert.True(tag != null && sprite.AnimationTags!.Any(x => x.Name == "idle"), "the tag was not added");
            Assert.True(h.UndoStackSize == before + 1,
                $"expected exactly one new undo step, stack went {before} -> {h.UndoStackSize}");

            h.Exec("Edit.Undo");
            Assert.True(sprite.AnimationTags?.Any(x => x.Name == "idle") != true, "undo did not remove the tag");
        });

        t.Check("export anchors round-trip through the editor and undo", () =>
        {
            h.SpriteEditor.SetExportPivot(new SKPoint(12, 30));
            h.SpriteEditor.SetNineSlice(new NineSliceMargins { Left = 4, Top = 4, Right = 4, Bottom = 4 });

            Assert.True(sprite.ExportPivot == new SKPoint(12, 30), $"pivot is {sprite.ExportPivot}");
            Assert.True(sprite.NineSlice is { Left: 4, Bottom: 4 }, "9-slice margins did not stick");

            h.Exec("Edit.Undo");
            Assert.True(sprite.NineSlice == null, "undo did not clear the 9-slice");
            h.Exec("Edit.Undo");
            Assert.True(sprite.ExportPivot == null, "undo did not clear the pivot");
        });

        t.Check("animation metadata survives a save/load round-trip", () =>
        {
            sprite.AnimationTags = [new SpriteAnimationTag
            {
                Name = "run", From = 1, To = 3, Direction = SpriteAnimationDirection.PingPong
            }];
            sprite.SetFrameDurationMs(1, 320);
            sprite.ExportPivot = new SKPoint(8, 15);
            sprite.NineSlice = new NineSliceMargins { Left = 2, Top = 3, Right = 4, Bottom = 5 };

            var scene = h.AppState.CurrentProject.SceneNode!;
            using var serializer = new NodeSerializer();
            var json = serializer.Serialize(scene);
            var reloaded = ProjectFormat.DeserializeScene(json, ProjectFormat.CurrentVersion, serializer.GetDataEntries());

            var loaded = reloaded.Nodes.OfType<Pix2dSprite>().First();
            var tag = loaded.AnimationTags?.SingleOrDefault();
            Assert.True(tag is { Name: "run", From: 1, To: 3, Direction: SpriteAnimationDirection.PingPong },
                "the tag did not survive the round-trip");
            Assert.True(loaded.GetFrameDurationMs(1) == 320, $"duration is {loaded.GetFrameDurationMs(1)} ms");
            Assert.True(loaded.ExportPivot == new SKPoint(8, 15), $"pivot is {loaded.ExportPivot}");
            Assert.True(loaded.NineSlice is { Left: 2, Top: 3, Right: 4, Bottom: 5 }, "9-slice did not survive");
        });

        t.Check("SceneIntegrity drops tags that no longer address a frame", () =>
        {
            var scene = new SKNode { Name = "Scene" };
            var damaged = Pix2dSprite.CreateFromBitmap(new SKBitmap(8, 8));   // exactly 1 frame
            damaged.AnimationTags =
            [
                new SpriteAnimationTag { Name = "ok", From = 0, To = 0 },
                new SpriteAnimationTag { Name = "stale", From = 7, To = 9 },
                new SpriteAnimationTag { Name = "", From = 0, To = 0 }
            ];
            damaged.FrameDurations = [40, 80, 120];   // longer than the frame list
            scene.Nodes.Add(damaged);

            SceneIntegrity.Repair(scene);

            Assert.True(damaged.AnimationTags?.Count == 1 && damaged.AnimationTags[0].Name == "ok",
                $"expected only 'ok' to survive, got [{string.Join(",", damaged.AnimationTags?.Select(x => x.Name) ?? [])}]");
            Assert.True((damaged.FrameDurations?.Count ?? 0) <= 1,
                $"durations were not trimmed to the frame count: [{string.Join(",", damaged.FrameDurations ?? [])}]");
        });

        h.NewProject();
    }

    // --- Scenario 7ad: user brush presets — save, delete, persist -----------------------------------
    // Modeled on the palette library: a settings-backed list appended after the built-in presets. The
    // parts worth asserting are the ones that fail silently: the AppSettings-property requirement (an
    // undeclared settings key is a dropped write), value-vs-reference identity when deleting (
    // BrushSettings has value equality, so List.Remove would happily take out a built-in twin), and
    // tolerance of a settings file written by another build.
    static void BrushPresetScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Brush preset scenario ===");

        var drawing = h.Services.GetRequiredService<IDrawingService>();
        var settings = h.Services.GetRequiredService<ISettingsService>();
        var state = h.AppState.SpriteEditorState;

        var builtInCount = state.BrushPresets.Count;
        t.Check("the shipped presets are all built-ins", () =>
        {
            Assert.True(builtInCount > 0, "no presets at all");
            Assert.True(state.BrushPresets.All(p => !p.IsUserPreset),
                "a built-in preset is flagged as a user preset");
        });

        t.Check("saving the current brush appends a user preset", () =>
        {
            // A distinctive setting no built-in has, so the dedupe path can't swallow it.
            state.CurrentBrushSettings = new Pix2d.Primitives.Drawing.BrushSettings
            {
                Brush = state.BrushPresets[0].Brush,
                Scale = 37,
                Opacity = 0.42f,
                Spacing = 2.5f,
                PressureAffectsSize = true
            };

            var saved = drawing.SaveCurrentBrushAsPreset();

            Assert.True(saved is { IsUserPreset: true }, "the returned preset is missing or not flagged");
            Assert.True(state.BrushPresets.Count == builtInCount + 1,
                $"presets {builtInCount} -> {state.BrushPresets.Count}, expected +1");
            Assert.True(ReferenceEquals(state.BrushPresets[^1], saved), "the preset was not appended last");
            Assert.True(Math.Abs(saved!.Scale - 37) < 0.001 && saved.PressureAffectsSize,
                "the saved preset did not carry the current settings");
        });

        t.Check("saving the same brush twice does not duplicate it", () =>
        {
            var again = drawing.SaveCurrentBrushAsPreset();
            Assert.True(state.BrushPresets.Count == builtInCount + 1,
                $"a duplicate was added: {state.BrushPresets.Count} presets");
            Assert.True(ReferenceEquals(again, state.BrushPresets[^1]),
                "saving an existing preset should return that preset");
        });

        t.Check("the preset reaches the settings file, not just memory", () =>
        {
            // A FRESH service reading the real file — this is what proves AppSettings.UserBrushPresets
            // exists. Without the property, SettingsService.Set is a logged no-op and everything above
            // would still pass while nothing survived a restart.
            var reader = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            Assert.True(reader.TryGet<List<BrushPresetData>>("UserBrushPresets", out var stored) && stored != null,
                "nothing was persisted under UserBrushPresets");
            Assert.True(stored!.Count == 1, $"expected 1 stored preset, got {stored.Count}");
            Assert.True(!string.IsNullOrEmpty(stored[0].Brush) && Math.Abs(stored[0].Scale - 37) < 0.001,
                $"the stored DTO is wrong: brush='{stored[0].Brush}' scale={stored[0].Scale}");
        });

        t.Check("deleting a built-in preset hides it, and Reset restores it without touching user presets", () =>
        {
            var builtIn = state.BrushPresets[0];
            var builtInId = builtIn.BuiltInId;
            Assert.True(builtInId != null, "the first built-in has no BuiltInId");

            Assert.True(drawing.DeleteBrushPreset(builtIn), "deleting a built-in was refused");
            Assert.True(state.BrushPresets.Count == builtInCount, "the row did not shrink by one");
            Assert.True(state.BrushPresets.All(p => p.BuiltInId != builtInId), "the hidden built-in is still in the row");

            var reader = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            Assert.True(reader.TryGet<List<string>>("HiddenBuiltInPresetIds", out var hiddenIds)
                        && hiddenIds != null && hiddenIds.Contains(builtInId!),
                "the hidden id was not persisted under HiddenBuiltInPresetIds");

            drawing.ResetBrushPresetsToDefaults();
            Assert.True(state.BrushPresets.Count(p => !p.IsUserPreset) == builtInCount,
                "reset did not restore the full built-in row");
            Assert.True(state.BrushPresets.Any(p => p.BuiltInId == builtInId), "the built-in did not come back");
            Assert.True(state.BrushPresets.Count(p => p.IsUserPreset) == 1,
                "reset touched a preset the user actually saved");

            // A second fresh reader: SettingsService loads its file once and caches it, so re-using `reader`
            // here would just replay its first (pre-reset) snapshot instead of proving the write landed.
            var reader2 = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            reader2.TryGet<List<string>>("HiddenBuiltInPresetIds", out var clearedIds);
            Assert.True((clearedIds?.Count ?? 0) == 0, "reset did not clear the hidden-ids list");
        });

        t.Check("deleting a user preset that is a value-twin of a built-in removes the right one", () =>
        {
            // BrushSettings has value equality, so List.Remove(twin) would take out the built-in. Save a
            // deliberate twin of built-in #1 by hand (SaveCurrentBrushAsPreset would dedupe it away).
            var twin = state.BrushPresets[1].Clone();
            twin.IsUserPreset = true;
            state.BrushPresets = [.. state.BrushPresets, twin];

            var before = state.BrushPresets.Count;
            Assert.True(drawing.DeleteBrushPreset(twin), "the twin was not deleted");
            Assert.True(state.BrushPresets.Count == before - 1, "the count did not drop by one");
            Assert.True(state.BrushPresets.Take(builtInCount).All(p => !p.IsUserPreset)
                        && state.BrushPresets.Count(p => !p.IsUserPreset) == builtInCount,
                "a built-in preset was removed instead of the user twin");
        });

        t.Check("deleting a user preset persists and clears its selection", () =>
        {
            var preset = state.BrushPresets.Last(p => p.IsUserPreset);
            state.CurrentPixelBrushPreset = preset;

            Assert.True(drawing.DeleteBrushPreset(preset), "the preset was not deleted");
            Assert.True(state.BrushPresets.Count == builtInCount, "the user preset is still in the row");
            Assert.True(state.CurrentPixelBrushPreset == null, "the deleted preset is still selected");

            var reader = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            reader.TryGet<List<BrushPresetData>>("UserBrushPresets", out var stored);
            Assert.True((stored?.Count ?? 0) == 0, $"the deletion was not persisted ({stored?.Count} left)");
        });

        t.Check("a preset stored by another build is skipped, not fatal", () =>
        {
            settings.Set("UserBrushPresets", new List<BrushPresetData>
            {
                new() { Brush = "square", Scale = 9999, Opacity = 5, Spacing = 3 },   // out-of-range values
                new() { Brush = "no-such-brush", Scale = 4 }                          // unknown brush key
            });

            drawing.InitBrushSettings();

            var user = state.BrushPresets.Where(p => p.IsUserPreset).ToArray();
            Assert.True(user.Length == 1, $"expected the unknown brush to be skipped, got {user.Length} presets");
            Assert.True(user[0].Scale <= 512 && user[0].Opacity <= 1f,
                $"out-of-range values were not clamped: scale={user[0].Scale} opacity={user[0].Opacity}");
        });

        t.Check("a restored preset actually draws at its own size", () =>
        {
            h.NewProject(64);
            var preset = state.BrushPresets.First(p => p.IsUserPreset);

            // Apply it the way the UI does — a clone, never the shared instance.
            var applied = preset.Clone();
            applied.Scale = 4;
            state.CurrentBrushSettings = applied;

            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(20, 20);

            var painted = h.NonEmptyPixels().Count();
            Assert.True(painted > 1, $"a 4px preset should cover more than one pixel, painted {painted}");
        });

        t.Check("no active selection yields no stamp preset", () =>
        {
            h.NewProject(8);
            Assert.True(!h.HasPixelSelection, "a fresh project should start without a selection");
            Assert.True(drawing.CreateBrushPresetFromSelection(useOriginalColors: true) == null,
                "a stamp preset was created without a selection");
        });

        t.Check("an original-colors stamp reproduces the captured pixels, ignoring the paint color", () =>
        {
            h.NewProject(8);
            state.CurrentBrushSettings = state.BrushPresets.First(p => p.BuiltInId == "square-1").Clone();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(3, 3);
            h.EnsurePixelSelection();

            var before = state.BrushPresets.Count;
            var stamp = drawing.CreateBrushPresetFromSelection(useOriginalColors: true);

            Assert.True(stamp is { IsUserPreset: true } &&
                        stamp.Brush is Pix2d.Plugins.Drawing.Brushes.ImageStampBrush { UseOriginalColors: true },
                "the returned preset is missing or not an original-colors image stamp");
            Assert.True(state.BrushPresets.Count == before + 1, "the stamp preset was not appended");

            var reader = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            Assert.True(reader.TryGet<List<BrushPresetData>>("UserBrushPresets", out var stored) && stored != null,
                "the stamp preset was not persisted");
            var stampData = stored!.FirstOrDefault(d => d.Brush == Pix2d.Plugins.Drawing.Brushes.BrushKeys.StampKey);
            Assert.True(stampData != null && !string.IsNullOrEmpty(stampData.StampImagePng) && stampData.StampUseOriginalColors,
                "the persisted DTO is missing its stamp image or the color-mode flag");

            // Round-trip through a fresh load, the way a restart would.
            drawing.InitBrushSettings();
            var restored = state.BrushPresets.LastOrDefault(p =>
                p.Brush is Pix2d.Plugins.Drawing.Brushes.ImageStampBrush { UseOriginalColors: true });
            Assert.True(restored != null, "the stamp preset did not survive InitBrushSettings");

            h.NewProject(16);
            var applied = restored!.Clone();
            applied.Scale = 8;
            state.CurrentBrushSettings = applied;
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Blue); // an original-colors stamp must ignore the current draw color
            h.DrawPixel(8, 8);

            var painted = h.NonEmptyPixels().ToArray();
            Assert.True(painted.Any(p => p.Color.Red > 200 && p.Color.Blue < 50),
                "an original-colors stamp should paint its own captured red, not the current draw color");

            // The preset tile hands GetPreviewBitmap's RAW pixel buffer to Avalonia (BitmapExtensions.ToBitmap),
            // so the restored stamp must be in the app's own color type — SKBitmap.Decode returns the platform's
            // native N32 (Bgra8888 on Windows/Android), which previewed red pixels as blue.
            var restoredBrush = (Pix2d.Plugins.Drawing.Brushes.ImageStampBrush)restored!.Brush!;
            Assert.True(restoredBrush.SourceBitmap.ColorType == Pix2d.Pix2DAppSettings.ColorType,
                $"a restored stamp is {restoredBrush.SourceBitmap.ColorType}, expected {Pix2d.Pix2DAppSettings.ColorType}");

            var tilePreview = restoredBrush.GetPreviewBitmap(restored.Scale);
            Assert.True(tilePreview.ColorType == Pix2d.Pix2DAppSettings.ColorType,
                $"the preset tile's preview is {tilePreview.ColorType}, expected {Pix2d.Pix2DAppSettings.ColorType}");
            Assert.True(tilePreview.Pixels.Any(p => p.Red > 200 && p.Blue < 50),
                "the preset tile previews the captured red as some other color");
        });

        t.Check("a recolorable stamp paints the current draw color, not the captured one", () =>
        {
            h.NewProject(8);
            state.CurrentBrushSettings = state.BrushPresets.First(p => p.BuiltInId == "square-1").Clone();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(3, 3);
            h.EnsurePixelSelection();

            var stamp = drawing.CreateBrushPresetFromSelection(useOriginalColors: false);
            Assert.True(stamp?.Brush is Pix2d.Plugins.Drawing.Brushes.ImageStampBrush { UseOriginalColors: false },
                "the recolorable stamp was not created as expected");

            h.NewProject(16);
            var applied = stamp!.Clone();
            applied.Scale = 8;
            state.CurrentBrushSettings = applied;
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Blue);
            h.DrawPixel(8, 8);

            var painted = h.NonEmptyPixels().ToArray();
            Assert.True(painted.Length > 0, "the recolorable stamp painted nothing");
            Assert.True(painted.All(p => p.Color.Blue > 200 && p.Color.Red < 50),
                "a recolorable stamp should paint the current draw color, not the captured one");
        });

        t.Check("every preset kind can render the settings panel's stroke preview", () =>
        {
            // The preview runs on a throwaway copy of the live brush (RenderStrokePreview mutates pressure and
            // the stamp cache). That copy used to come from Activator.CreateInstance(brush.GetType()), which
            // threw MissingMethodException the moment a stamp preset was selected — a stamp has no parameterless
            // ctor. Cover every preset the row can hold, stamps included.
            h.NewProject(8);
            state.CurrentBrushSettings = state.BrushPresets.First(p => p.BuiltInId == "square-1").Clone();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(3, 3);
            h.EnsurePixelSelection();

            var stamp = drawing.CreateBrushPresetFromSelection(useOriginalColors: false);
            Assert.True(stamp?.Brush is Pix2d.Plugins.Drawing.Brushes.ImageStampBrush,
                "precondition: no stamp preset to preview");

            foreach (var preset in state.BrushPresets)
            {
                var live = preset.Brush as Pix2d.Plugins.Drawing.Brushes.BasePixelBrush;
                Assert.True(live != null, $"preset '{preset.BuiltInId ?? "user"}' has no BasePixelBrush");

                using var previewBrush = live!.CreatePreviewInstance();
                Assert.True(!ReferenceEquals(previewBrush, live),
                    "the preview must not run on the instance the canvas draws with");

                previewBrush.InitBrush(preset.Scale, preset.Opacity, preset.Spacing);
                using var preview = previewBrush.RenderStrokePreview(64, 24, SKColors.Blue,
                    Pix2d.Plugins.Drawing.Brushes.BrushPreviewBackground.White);
                Assert.True(preview.Width == 64 && preview.Height == 24,
                    $"preview came back {preview.Width}x{preview.Height}");
            }

            // The stamp's copy shares the captured bitmap without owning it, so disposing the copies above must
            // leave the preset able to paint.
            h.NewProject(16);
            var applied = stamp!.Clone();
            applied.Scale = 8;
            state.CurrentBrushSettings = applied;
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Blue);
            h.DrawPixel(8, 8);

            Assert.True(h.NonEmptyPixels().Any(),
                "the stamp preset stopped painting after its preview copy was disposed");
        });

        // The two halves of "the new preset is selected" are separate state: CurrentPixelBrushPreset is the
        // row's highlight, CurrentBrushSettings is what the canvas draws with. The presets menu wrote only the
        // former, so a freshly created preset looked active while every stroke still used the previous brush.
        // Asserted through BrushSettingsView.State (plain view-model state, no Avalonia types) because that is
        // where the menu items land — the service deliberately doesn't activate anything.
        t.Check("creating a brush from a selection makes it the brush that actually draws", () =>
        {
            h.NewProject(8);
            state.CurrentBrushSettings = state.BrushPresets.First(p => p.BuiltInId == "square-1").Clone();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            h.DrawPixel(3, 3);
            h.EnsurePixelSelection();

            var vm = new Pix2d.UI.BrushSettings.BrushSettingsView.State(h.AppState, drawing, h.Dialogs);

            // Both color modes go through the same code path, so both used to be broken; check both.
            foreach (var useOriginalColors in new[] { true, false })
            {
                vm.SaveSelectionAsBrushPreset(useOriginalColors);

                var created = state.BrushPresets[^1];
                Assert.True(created.Brush is Pix2d.Plugins.Drawing.Brushes.ImageStampBrush,
                    $"precondition: no stamp preset was created (useOriginalColors={useOriginalColors})");
                Assert.True(ReferenceEquals(state.CurrentPixelBrushPreset, created),
                    $"the new preset is not selected in the row (useOriginalColors={useOriginalColors})");
                Assert.True(ReferenceEquals(state.CurrentBrushSettings.Brush, created.Brush),
                    $"the live brush is still the previous one (useOriginalColors={useOriginalColors})");
                Assert.True(!ReferenceEquals(state.CurrentBrushSettings, created),
                    "the live settings must be a clone, not the preset tile itself");
            }

            // And the same for saving the current brush, which shares the activation helper.
            state.CurrentBrushSettings = state.BrushPresets.First(p => p.BuiltInId == "circle-8").Clone();
            vm.SaveCurrentBrushAsPreset();
            Assert.True(state.CurrentPixelBrushPreset != null &&
                        ReferenceEquals(state.CurrentBrushSettings.Brush, state.CurrentPixelBrushPreset.Brush),
                "saving the current brush left the row highlight and the live brush out of sync");
        });

        // Leave no user presets and no hidden built-ins behind for the command sweep / later scenarios.
        settings.Set("UserBrushPresets", new List<BrushPresetData>());
        settings.Set("HiddenBuiltInPresetIds", new List<string>());
        drawing.InitBrushSettings();
        h.NewProject();
    }

    // --- Scenario 7ba: wheel source detection (trackpad vs. mouse wheel) ----------------------------
    // Pure logic, no app state: the classifier SkiaCanvas.OnPointerWheelChanged uses to decide whether the
    // "mouse wheel behavior" setting applies. Deltas below are what the platforms actually report.
    static void PrecisionScrollDetectorScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Wheel source detection scenario ===");

        t.Check("a notched mouse wheel is never read as a trackpad", () =>
        {
            var d = new PrecisionScrollDetector();
            ulong ts = 1000;
            for (var i = 0; i < 5; i++, ts += 300) // one notch every 300 ms
                Assert.True(!d.Observe(0, i % 2 == 0 ? 1 : -1, ts), $"notch #{i} was classified as precision");
        });

        t.Check("a high-resolution mouse wheel is not read as a trackpad either", () =>
        {
            // Logitech SmartShift & co: sub-notch fractions on a single axis, in fast bursts. Ambiguous by
            // delta shape alone, so the detector deliberately requires two-axis movement — a fractional
            // single-axis stream must NOT override the user's "wheel = zoom" setting.
            var d = new PrecisionScrollDetector();
            ulong ts = 1000;
            for (var i = 0; i < 12; i++, ts += 8)
                Assert.True(!d.Observe(0, 0.125, ts), $"high-res tick #{i} was classified as precision");
        });

        t.Check("a diagonal two-finger scroll latches to precision", () =>
        {
            var d = new PrecisionScrollDetector();
            Assert.True(!d.Observe(0, 0.2, 1000), "a single vertical fraction should not be enough evidence");
            Assert.True(d.Observe(0.05, 0.31, 1008), "diagonal delta was not recognized as precision");
            // The latch holds through the perfectly-vertical part of the same gesture and its inertia tail.
            Assert.True(d.Observe(0, 0.28, 1016), "latch dropped on a vertical delta inside the gesture");
            Assert.True(d.Observe(0, 0.04, 1150), "latch dropped on the inertia tail");
        });

        t.Check("a touchpad pinch gesture also settles it", () =>
        {
            var d = new PrecisionScrollDetector();
            d.NotifyTouchPadGesture();
            Assert.True(d.IsPrecisionScrolling, "pinch gesture did not mark the source as precision");
            Assert.True(d.Observe(0, 0.2, 1000), "latch dropped right after a pinch");
        });

        t.Check("plugging in a mouse after using the trackpad switches back", () =>
        {
            var d = new PrecisionScrollDetector();
            Assert.True(d.Observe(0.1, 0.4, 1000), "precondition: not latched to precision");
            // A whole step on one axis, after a pause no hand can beat: that is a real notch.
            Assert.True(!d.Observe(0, 1, 1400), "a wheel notch after a pause did not release the latch");
        });

        t.Check("a whole-number delta inside a trackpad burst does not release the latch", () =>
        {
            var d = new PrecisionScrollDetector();
            d.Observe(0.1, 0.4, 1000);
            Assert.True(d.Observe(0, 1, 1012), "latch released mid-burst by a whole-number delta");
        });
    }

    // --- Scenario 7bb: pixel selection — marquee drag vs. click, and the Deselect command -----------
    // Runs on a *second* artboard on purpose: the active sprite then sits away from the scene origin,
    // which is what exposed the world-vs-layer-local mix-up in SelectionController.BeginSelection (magic
    // wand sampled the wrong pixel, lasso/click dragged a stray line into the selection).
    static void PixelSelectionScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Pixel selection scenario (click = deselect) ===");

        h.NewProject(64);
        h.Exec("Sprite.Edit.AddArtboard");
        h.SetView(1);

        // Keep the marquee tool active after a selection — the auto-open transform editor would hand the
        // gesture to PixelTransformTool, and these checks are about the selection tool's own click path.
        var autoTransform = h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled;
        h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled = false;

        var sprite = h.ActiveSprite;
        var box = sprite.GetBoundingBox();
        Console.WriteLine($"  [diag] active artboard bounds: {box}");

        t.Check("premise: the active artboard is not at the scene origin", () =>
            Assert.True(box.Left > 0, $"expected an offset artboard, bounds = {box}"));

        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectRectTool>();

        t.Check("rect drag creates a marquee inside the artboard", () =>
        {
            h.DragWorld(box.Left + 4, box.Top + 4, box.Left + 20, box.Top + 20);
            Assert.True(h.HasPixelSelection, "no selection after the drag");
            var sel = h.PixelSelectionBounds;
            Console.WriteLine($"  [diag] marquee bounds: {sel}");
            Assert.True(box.Contains(sel), $"marquee {sel} is not inside the artboard {box}");
        });

        // A drag running past the canvas edge used to size the selector's mask buffer from the raw drag
        // extent (`new byte[_width * _height]` over the collected points), so a few hundred DIP off a
        // zoomed-out canvas asked for hundreds of megabytes and died mid-gesture with OutOfMemoryException
        // (appstat, 3.11.2, Android on a 64x64 canvas). The marquee must clamp to the canvas instead.
        // 4000 world units is kept deliberately modest so a regression fails fast instead of thrashing.
        t.Check("a drag far off-canvas clamps the marquee to the canvas", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            h.DragWorld(box.Left + 4, box.Top + 4, box.Left + 4000, box.Top + 4000);
            Assert.True(h.HasPixelSelection, "no selection after the off-canvas drag");
            var sel = h.PixelSelectionBounds;
            Console.WriteLine($"  [diag] off-canvas drag marquee bounds: {sel}");
            Assert.True(box.Contains(sel), $"marquee {sel} escaped the artboard {box}");
            h.Exec("Edit.Selection.Deselect"); // this marquee covers most of the canvas — don't leak it
        });

        t.Check("click outside the marquee clears the selection", () =>
        {
            h.ClickWorld(box.Left + 40, box.Top + 40);
            Assert.True(!h.HasPixelSelection, "selection survived a click outside it");
        });

        t.Check("click inside the marquee keeps the selection", () =>
        {
            h.DragWorld(box.Left + 4, box.Top + 4, box.Left + 20, box.Top + 20);
            var sel = h.PixelSelectionBounds;
            h.ClickWorld(sel.MidX, sel.MidY);
            Assert.True(h.HasPixelSelection, "clicking the selected area dropped the selection");
        });

        t.Check("click outside the canvas clears the selection", () =>
        {
            h.ClickWorld(box.MidX, box.Bottom + 30);
            Assert.True(!h.HasPixelSelection, "selection survived a click off-canvas");
        });

        t.Check("Deselect command (Ctrl+D) drops a marquee", () =>
        {
            h.DragWorld(box.Left + 4, box.Top + 4, box.Left + 20, box.Top + 20);
            Assert.True(h.HasPixelSelection, "precondition: no marquee to deselect");
            h.Exec("Edit.Selection.Deselect");
            Assert.True(!h.HasPixelSelection, "Deselect left the marquee in place");
        });

        t.Check("lasso drag stays inside the artboard", () =>
        {
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectLassoTool>();
            h.PressWorld(box.Left + 6, box.Top + 6);
            h.MoveWorld(box.Left + 24, box.Top + 8, pressed: true);
            h.MoveWorld(box.Left + 20, box.Top + 26, pressed: true);
            h.ReleaseWorld(box.Left + 20, box.Top + 26);
            Assert.True(h.HasPixelSelection, "no selection after the lasso drag");
            var sel = h.PixelSelectionBounds;
            Console.WriteLine($"  [diag] lasso bounds: {sel}");
            Assert.True(box.Contains(sel), $"lasso selection {sel} is not inside the artboard {box}");
        });

        t.Check("magic wand still selects on a single click", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectColorTool>();
            h.ClickWorld(box.MidX, box.MidY);
            Assert.True(h.HasPixelSelection, "magic-wand click produced no selection");
            var sel = h.PixelSelectionBounds;
            Console.WriteLine($"  [diag] wand bounds: {sel}");
            Assert.True(box.Contains(sel), $"wand selection {sel} is not inside the artboard {box}");
        });

        t.Check("magic-wand click off-canvas clears the selection", () =>
        {
            h.ClickWorld(box.MidX, box.Bottom + 30);
            Assert.True(!h.HasPixelSelection, "selection survived an off-canvas wand click");
        });

        // Ctrl+D on lifted pixels: the transform must be committed (via the hand-off back to the marquee
        // tool) rather than silently dropped, and the user must not be left in a tool with nothing to do.
        h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled = true;
        t.Check("Deselect on a live transform commits and leaves the transform tool", () =>
        {
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectRectTool>();
            h.DragWorld(box.Left + 4, box.Left + 4, box.Left + 20, box.Top + 20);
            Console.WriteLine($"  [diag] after drag: tool={h.AppState.ToolsState.CurrentToolKey}, phase={h.PixelSelectionPhase}");
            Assert.True(h.PixelSelectionPhase == Pix2d.Primitives.Drawing.SelectionPhase.Transforming,
                $"precondition: expected lifted pixels, phase = {h.PixelSelectionPhase}");

            h.Exec("Edit.Selection.Deselect");
            Assert.True(!h.HasPixelSelection, "Deselect left the marquee in place");
            Assert.True(h.AppState.ToolsState.CurrentToolKey != nameof(Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelTransformTool),
                "still on PixelTransformTool after Deselect");
        });

        h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled = autoTransform;
    }

    // --- Scenario 7bc: combining marquees — Shift adds, Ctrl subtracts, Shift+Ctrl intersects -------
    // The selection has two representations (a live SpriteSelectionNode with its own transform, and a flat
    // canvas mask) and only the second one can be combined, so every check here asserts on the *mask*
    // (h.IsPixelSelected / h.SelectedPixelCount) rather than on the bounding box — a union and the
    // rectangle enclosing it have the same bounds, and a subtracted hole has none at all.
    // Runs on a second artboard so the drawing target is away from the scene origin, which is where the
    // mask rasterization has to subtract the target position or land the region in the wrong place.
    static void SelectionCombineScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Selection combine scenario (Shift / Ctrl) ===");

        h.NewProject(64);
        h.Exec("Sprite.Edit.AddArtboard");
        h.SetView(1);

        // Lifting pixels into the transform tool ends contour mode, and a lifted selection deliberately
        // degrades combining back to Replace — keep the marquee tool in charge for these checks.
        var autoTransform = h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled;
        h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled = false;

        var box = h.ActiveSprite.GetBoundingBox();
        var l = box.Left;
        var tp = box.Top;

        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectRectTool>();

        // Two disjoint 9x9 squares: pixels 4..12 and 20..28.
        void DragA(KeyModifier m = KeyModifier.None) => h.DragWorld(l + 4, tp + 4, l + 12, tp + 12, m);
        void DragB(KeyModifier m = KeyModifier.None) => h.DragWorld(l + 20, tp + 20, l + 28, tp + 28, m);

        t.Check("a plain second marquee still replaces the first", () =>
        {
            DragA();
            DragB();
            Assert.True(!h.IsPixelSelected(6, 6), "the first marquee survived a plain (no-modifier) redraw");
            Assert.True(h.IsPixelSelected(24, 24), "the second marquee did not take");
            Assert.True(h.SelectedPixelCount() == 81, $"expected 81 selected pixels, got {h.SelectedPixelCount()}");
        });

        t.Check("Shift+drag unions the new marquee with the existing selection", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            DragA();
            DragB(KeyModifier.Shift);

            Assert.True(h.IsPixelSelected(6, 6), "the original region was dropped instead of added to");
            Assert.True(h.IsPixelSelected(24, 24), "the added region is not selected");
            Assert.True(!h.IsPixelSelected(16, 16), "the gap between the two squares got selected");
            Assert.True(h.SelectedPixelCount() == 162, $"expected 81+81 selected pixels, got {h.SelectedPixelCount()}");
        });

        t.Check("undo of a Shift-add steps back to the previous selection, not to nothing", () =>
        {
            h.Operations.Undo();
            Assert.True(h.HasPixelSelection, "undo cleared the selection the add was built on");
            Assert.True(h.IsPixelSelected(6, 6), "the original region did not come back");
            Assert.True(!h.IsPixelSelected(24, 24), "the added region survived undo");
        });

        t.Check("Ctrl+drag subtracts the new marquee from the existing selection", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            DragA();
            // Bite the bottom-right quadrant (8..12) out of the 4..12 square.
            h.DragWorld(l + 8, tp + 8, l + 12, tp + 12, KeyModifier.Ctrl);

            Assert.True(h.IsPixelSelected(5, 5), "the untouched part of the selection was dropped");
            Assert.True(!h.IsPixelSelected(10, 10), "the subtracted region is still selected");
            Assert.True(h.SelectedPixelCount() == 81 - 25, $"expected 56 selected pixels, got {h.SelectedPixelCount()}");
        });

        t.Check("Shift+Ctrl keeps only the overlap", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            DragA();
            // 8..20 overlaps 4..12 on 8..12 — a 5x5 square.
            h.DragWorld(l + 8, tp + 8, l + 20, tp + 20, KeyModifier.Shift | KeyModifier.Ctrl);

            Assert.True(h.IsPixelSelected(10, 10), "the overlap is not selected");
            Assert.True(!h.IsPixelSelected(5, 5), "a pixel outside the new marquee stayed selected");
            Assert.True(!h.IsPixelSelected(16, 16), "a pixel outside the old marquee got selected");
            Assert.True(h.SelectedPixelCount() == 25, $"expected 25 selected pixels, got {h.SelectedPixelCount()}");
        });

        // A subtracted hole is a second sub-contour inside the outer one, so the next gesture has to
        // rasterize the base as a donut rather than as a solid rectangle.
        t.Check("a hole punched out of a selection survives a further add", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            h.DragWorld(l + 4, tp + 4, l + 16, tp + 16);                            // 13x13 = 169
            h.DragWorld(l + 8, tp + 8, l + 12, tp + 12, KeyModifier.Ctrl);          // minus 5x5 = 25
            Assert.True(!h.IsPixelSelected(10, 10), "precondition: the hole was not punched");

            DragB(KeyModifier.Shift);                                               // plus 9x9 = 81
            Assert.True(!h.IsPixelSelected(10, 10), "the hole filled itself back in");
            Assert.True(h.IsPixelSelected(5, 5) && h.IsPixelSelected(24, 24), "the donut or the addition was lost");
            Assert.True(h.SelectedPixelCount() == 169 - 25 + 81,
                $"expected 225 selected pixels, got {h.SelectedPixelCount()}");
        });

        t.Check("subtracting the whole selection away leaves nothing selected", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            DragA();
            h.DragWorld(l + 2, tp + 2, l + 30, tp + 30, KeyModifier.Ctrl);
            Assert.True(!h.HasPixelSelection, "a fully subtracted selection is still live");
        });

        // BeginSelection drops the live marquee before the new one is drawn, so a combining gesture that
        // produces nothing has to put it back — otherwise Shift+click, which asks to *add*, would clear.
        t.Check("Shift+click keeps the selection instead of deselecting", () =>
        {
            DragA();
            h.ClickWorld(l + 40, tp + 40, KeyModifier.Shift);
            Assert.True(h.HasPixelSelection, "Shift+click outside the marquee cleared the selection");
            Assert.True(h.IsPixelSelected(6, 6), "Shift+click changed which pixels are selected");
            Assert.True(h.SelectedPixelCount() == 81, $"expected the selection untouched, got {h.SelectedPixelCount()}");
        });

        // With nothing to combine against there is no sensible set operation, so the gesture degrades to a
        // plain marquee (same as Photoshop) rather than doing nothing at all.
        t.Check("a modifier with nothing selected behaves like a plain marquee", () =>
        {
            foreach (var modifier in new[] { KeyModifier.Shift, KeyModifier.Ctrl, KeyModifier.Shift | KeyModifier.Ctrl })
            {
                h.Exec("Edit.Selection.Deselect");
                DragB(modifier);
                Assert.True(h.IsPixelSelected(24, 24), $"{modifier}+drag on an empty selection produced nothing");
                Assert.True(h.SelectedPixelCount() == 81,
                    $"{modifier}: expected 81 selected pixels, got {h.SelectedPixelCount()}");
            }
        });

        t.Check("lasso adds to a rectangle marquee", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            DragA();

            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectLassoTool>();
            h.PressWorld(l + 20, tp + 20, KeyModifier.Shift);
            h.MoveWorld(l + 30, tp + 22, pressed: true, KeyModifier.Shift);
            h.MoveWorld(l + 26, tp + 30, pressed: true, KeyModifier.Shift);
            h.ReleaseWorld(l + 26, tp + 30, KeyModifier.Shift);

            Assert.True(h.IsPixelSelected(6, 6), "the rectangle was dropped by the lasso add");
            Assert.True(h.SelectedPixelCount() > 81, $"the lasso added nothing, count = {h.SelectedPixelCount()}");
        });

        // The magic wand is the case this feature exists for: picking several colour regions one click at
        // a time. It also takes a different code path — a click, not a drag, and a selector whose mask is
        // whole-canvas rather than bounding-box sized.
        t.Check("magic wand Shift+click adds a second colour region", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            // A 2x2 blob, not a single pixel: FinishSelection drops a selection whose bitmap is 1x1.
            h.DrawPixel((int)l + 10, (int)tp + 10);
            h.DrawPixel((int)l + 11, (int)tp + 10);
            h.DrawPixel((int)l + 10, (int)tp + 11);
            h.DrawPixel((int)l + 11, (int)tp + 11);

            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectColorTool>();
            h.ClickWorld(l + 10.5f, tp + 10.5f);
            var drawnRegion = h.SelectedPixelCount();
            Assert.True(drawnRegion > 0 && h.IsPixelSelected(10, 10), "the wand did not select the drawn pixel");
            Assert.True(!h.IsPixelSelected(40, 40), "the wand leaked into the transparent area");

            h.ClickWorld(l + 40.5f, tp + 40.5f, KeyModifier.Shift);
            Assert.True(h.IsPixelSelected(10, 10), "the first colour region was dropped");
            Assert.True(h.IsPixelSelected(40, 40), "the second colour region was not added");
            Assert.True(h.SelectedPixelCount() == 64 * 64,
                $"drawn + transparent should cover the canvas, got {h.SelectedPixelCount()}");
        });

        // Invert flattens the live selection through the same rasterization the combining does, and it used
        // to apply the layer transform to a contour path that was already in canvas coordinates — so a
        // *non-rectangular* selection (lasso / wand, the ones that carry a path) inverted around a region
        // offset by twice its own position. A rectangle marquee has no path and was never affected, which
        // is what hid it.
        t.Check("invert of a contour selection is the exact complement", () =>
        {
            h.Exec("Edit.Selection.Deselect");
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.PixelSelect.PixelSelectColorTool>();
            h.ClickWorld(l + 10.5f, tp + 10.5f);       // the 2x2 red blob drawn above

            var before = h.SelectedPixelCount();
            Assert.True(before == 4, $"precondition: expected the 2x2 blob selected, got {before}");

            h.Exec("Edit.Selection.InvertSelection");
            Assert.True(!h.IsPixelSelected(10, 10), "the originally selected pixel is still selected");
            Assert.True(h.IsPixelSelected(40, 40), "the complement does not cover the rest of the canvas");
            Assert.True(h.SelectedPixelCount() == 64 * 64 - 4,
                $"expected the exact complement, got {h.SelectedPixelCount()}");
        });

        h.Exec("Edit.Selection.Deselect");
        h.AppState.IsAutoOpenTransformEditorAfterSelectionEnabled = autoTransform;
    }

    // --- Scenario 7b: General (objects) context + ObjectManipulationTool ----------------------------
    // Covers: default-tool activation on context switch, click/Shift+click selection (incl. the
    // MoveThumb Shift pass-through), select-and-drag in one gesture (one undoable MoveOperation),
    // rubber-band selection, and double-click diving back into the Sprite context.
    static void GeneralContextObjectToolScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== General context / ObjectManipulationTool scenario ===");

        h.NewProject(64);
        h.Exec("Sprite.Edit.AddArtboard"); // second artboard, laid out to the right of the first

        var sprites = h.AppState.CurrentProject.SceneNode!.Nodes.OfType<Pix2d.CommonNodes.Pix2dSprite>().ToArray();
        var a = sprites[0];
        var b = sprites[1];
        var aBox = a.GetBoundingBox();
        var bBox = b.GetBoundingBox();

        SkiaNodes.SKNode[] Sel() => h.AppState.CurrentProject.Selection?.Nodes ?? [];

        h.AppState.CurrentProject.CurrentContextType = EditContextType.General;

        // Pin the camera 1:1 — AddArtboard's ShowAll zooms far out to fit both artboards into the 64px
        // harness viewport, which blows the screen-pixel-sized thumb hit zones up to artboard scale.
        h.SetView(1);

        t.Check("switching to General activates ObjectManipulationTool by default", () =>
            Assert.True(h.AppState.ToolsState.CurrentToolKey == nameof(Pix2d.Tools.ObjectManipulationTool),
                $"current tool = {h.AppState.ToolsState.CurrentToolKey}"));

        t.Check("click selects the artboard under the cursor", () =>
        {
            h.ClickWorld(aBox.MidX, aBox.MidY);
            Assert.True(Sel().Length == 1 && Sel()[0] == a,
                $"selection = [{string.Join(", ", Sel().Select(n => n.Name))}]");
        });

        t.Check("Shift+click adds the second artboard to the selection", () =>
        {
            h.ClickWorld(bBox.MidX, bBox.MidY, KeyModifier.Shift);
            Assert.True(Sel().Length == 2 && Sel().Contains(a) && Sel().Contains(b),
                $"selection = [{string.Join(", ", Sel().Select(n => n.Name))}]");
        });

        t.Check("Shift+click on a selected artboard removes it (MoveThumb pass-through)", () =>
        {
            h.PressWorld(bBox.MidX, bBox.MidY, KeyModifier.Shift);
            Console.WriteLine($"  [diag] after shift-press: captured={SKInput.Current.CapturedPointerBy?.GetType().Name ?? "null"}, sel=[{string.Join(", ", Sel().Select(n => n.Name))}]");
            h.ReleaseWorld(bBox.MidX, bBox.MidY, KeyModifier.Shift);
            Assert.True(Sel().Length == 1 && Sel()[0] == a,
                $"selection = [{string.Join(", ", Sel().Select(n => n.Name))}]");
        });

        t.Check("click on empty canvas clears the selection", () =>
        {
            h.PressWorld(aBox.MidX, aBox.Bottom + 40);
            Console.WriteLine($"  [diag] after empty-press: captured={SKInput.Current.CapturedPointerBy?.GetType().Name ?? "null"}, sel=[{string.Join(", ", Sel().Select(n => n.Name))}]");
            h.ReleaseWorld(aBox.MidX, aBox.Bottom + 40);
            Assert.True(Sel().Length == 0, $"selection = [{string.Join(", ", Sel().Select(n => n.Name))}]");
        });

        var posBefore = a.Position;
        t.Check("press + drag on an unselected artboard selects and moves it in one gesture", () =>
        {
            var undoBefore = h.Operations.UndoOperationsCount;
            // 10.4/5.4: the move thumb floors the world-space delta (SnapToPixels), so a fractional
            // offset lands exactly on +10/+5 regardless of float noise in the viewport round-trip.
            h.PressWorld(aBox.MidX, aBox.MidY);
            h.MoveWorld(aBox.MidX + 10.4f, aBox.MidY + 5.4f, pressed: true);
            h.ReleaseWorld(aBox.MidX + 10.4f, aBox.MidY + 5.4f);

            Assert.True(Sel().Length == 1 && Sel()[0] == a, "artboard was not selected by the drag gesture");
            Assert.True(a.Position == new SKPoint(posBefore.X + 10, posBefore.Y + 5),
                $"position {posBefore} -> {a.Position}, expected +10/+5");
            Assert.True(h.Operations.UndoOperationsCount == undoBefore + 1,
                $"undo count {undoBefore} -> {h.Operations.UndoOperationsCount}, expected +1 (one MoveOperation)");
        });

        t.Check("undo restores the artboard position", () =>
        {
            h.Operations.Undo();
            Assert.True(a.Position == posBefore, $"position after undo = {a.Position}, expected {posBefore}");
        });

        t.Check("rubber-band drag selects every artboard it touches", () =>
        {
            var left = aBox.Left - 10;
            var top = aBox.Top - 40; // start above the artboards (also above their name labels)
            h.PressWorld(left, top);
            h.MoveWorld(bBox.Right + 10, bBox.Bottom + 10, pressed: true);
            h.ReleaseWorld(bBox.Right + 10, bBox.Bottom + 10);
            Assert.True(Sel().Length == 2 && Sel().Contains(a) && Sel().Contains(b),
                $"selection = [{string.Join(", ", Sel().Select(n => n.Name))}]");
        });

        t.Check("double-click on an artboard dives back into the Sprite context for it", () =>
        {
            try
            {
                h.ClickWorld(bBox.MidX, bBox.MidY, clickCount: 2);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [diag] double-click threw:\n" + ex);
                throw;
            }
            Assert.True(h.AppState.CurrentProject.CurrentContextType == EditContextType.Sprite,
                $"context = {h.AppState.CurrentProject.CurrentContextType}");
            Assert.True(ReferenceEquals(h.AppState.CurrentProject.CurrentEditedNode, b),
                "CurrentEditedNode is not the double-clicked artboard");
            Assert.True(h.AppState.ToolsState.CurrentToolKey == nameof(Pix2d.Plugins.Drawing.Tools.BrushTool),
                $"current tool = {h.AppState.ToolsState.CurrentToolKey}");
        });
    }

    // --- Scenario 7c: General-context object commands + canvas-edit sub-modes -----------------------
    // Covers: entering General by double-clicking an artboard's name label, the confirmed + undoable
    // delete (including the survivor re-target), the name-grouped Arrange packing, and the Resize / Crop
    // sessions owned by IArtboardObjectEditService.
    static void GeneralContextObjectCommandsScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== General context object commands scenario ===");

        h.NewProject(64);
        h.Exec("Sprite.Edit.AddArtboard");
        h.Exec("Sprite.Edit.AddArtboard");
        h.SetView(1); // AddArtboard's ShowAll zooms out; gestures need 1:1 (see the tool scenario)

        var artboards = h.Artboards;
        Console.WriteLine($"  [diag] artboards: {string.Join(", ", artboards.Select(a => $"{a.Name}@{a.Position}"))}");

        t.Check("double-clicking an artboard's name label enters the General context", () =>
        {
            h.AppState.CurrentProject.CurrentContextType = EditContextType.Sprite;
            h.ClickArtboardLabel(artboards[1], clickCount: 2);

            Assert.True(h.AppState.CurrentProject.CurrentContextType == EditContextType.General,
                $"context = {h.AppState.CurrentProject.CurrentContextType}");
            Assert.True(h.SelectedNodes.Length == 1 && h.SelectedNodes[0] == artboards[1],
                $"selection = [{string.Join(", ", h.SelectedNodes.Select(n => n.Name))}]");
            Assert.True(ReferenceEquals(h.AppState.CurrentProject.CurrentEditedNode, artboards[1]),
                "the label's artboard did not become the edit target");
        });

        // Labels are drawn at a fixed *on-screen* size, so zooming out inflates them in world space until
        // they bury each other and the artboards. ArtboardLabelsLayer then declutters: everything that is
        // not pinned (active / selected / hovered) drops out — and since hit-testing runs through the same
        // pass, a dropped label is not a hidden click target either.
        t.Check("zoomed out, an unpinned artboard's label is dropped — click target included", () =>
        {
            h.SetView(0.1f); // 64px artboards -> ~6px on screen, well under the 24px cutoff
            h.ClickArtboardLabel(artboards[2]);
            Assert.True(!ReferenceEquals(h.AppState.CurrentProject.CurrentEditedNode, artboards[2]),
                "a label hidden by the declutter pass is still clickable");

            h.SetView(1);
            h.ClickArtboardLabel(artboards[2]);
            Assert.True(ReferenceEquals(h.AppState.CurrentProject.CurrentEditedNode, artboards[2]),
                "at 1:1 the label should activate its artboard");

            h.ClickArtboardLabel(artboards[1], clickCount: 2); // back to the state the previous check left
        });

        // --- Delete: declined, then confirmed, then undone ----------------------------------------
        t.Check("declining the delete confirmation leaves the scene untouched", () =>
        {
            h.Dialogs.YesNoAnswer = false;
            h.SelectNodes(artboards[2]);
            h.Exec("Edit.Delete");

            Assert.True(h.Artboards.Length == 3, $"artboards = {h.Artboards.Length}, expected 3");
            Assert.True(h.Dialogs.LastYesNoMessage?.Contains(artboards[2].Name!) == true,
                $"confirmation did not name the object: {h.Dialogs.LastYesNoMessage}");
        });

        t.Check("confirmed delete removes the objects as one undoable step", () =>
        {
            h.Dialogs.YesNoAnswer = true;
            var undoBefore = h.Operations.UndoOperationsCount;
            h.SelectNodes(artboards[2]);
            h.Exec("Edit.Delete");

            Assert.True(h.Artboards.Length == 2, $"artboards = {h.Artboards.Length}, expected 2");
            Assert.True(h.Operations.UndoOperationsCount == undoBefore + 1,
                $"undo count {undoBefore} -> {h.Operations.UndoOperationsCount}, expected +1");
            Assert.True(h.SelectedNodes.Length == 0, "selection should be cleared after a delete");
        });

        t.Check("undo restores the deleted object", () =>
        {
            h.Operations.Undo();
            Assert.True(h.Artboards.Length == 3, $"artboards = {h.Artboards.Length}, expected 3");
        });

        t.Check("deleting the active artboard re-targets a survivor", () =>
        {
            var victim = (Pix2d.CommonNodes.Pix2dSprite)h.AppState.CurrentProject.CurrentEditedNode!;
            h.SelectNodes(victim);
            h.Exec("Edit.Delete");

            var target = h.AppState.CurrentProject.CurrentEditedNode;
            Assert.True(!ReferenceEquals(target, victim), "edit target still points at the deleted artboard");
            Assert.True(target is Pix2d.CommonNodes.Pix2dSprite s && h.Artboards.Contains(s),
                "edit target is not a surviving artboard");
            h.Operations.Undo();
        });

        // --- Arrange (grid packing, grouped by shared name prefix) --------------------------------
        t.Check("Arrange packs the selection into a dense grid grouped by name prefix, one undo step", () =>
        {
            h.NewProject(64);
            h.Exec("Sprite.Edit.AddArtboard");
            h.Exec("Sprite.Edit.AddArtboard");
            h.Exec("Sprite.Edit.AddArtboard");
            h.SetView(1);

            var all = h.Artboards;
            // AddArtboard lays them out in one row with a 16px gap: x = 0, 80, 160, 240.
            // Names drive the grouping: "icon-goal" (2 members), "icon" (icon-star-empty alone — no other
            // selected icon-star*), then the prefix-less bucket ("hero").
            all[0].Name = "icon-star-empty";
            all[1].Name = "hero";
            all[2].Name = "icon-goal-ice";
            all[3].Name = "icon-goal-gem";
            h.SelectNodes(all.Cast<SkiaNodes.SKNode>().ToArray());

            var undoBefore = h.Operations.UndoOperationsCount;
            h.Exec("Edit.Arrange.Arrange");

            // ceil(sqrt(4)) = 2 columns. Groups are ordered by their first member's name and stacked with a
            // 48px gutter (3x the 16px in-group gap):
            //   icon-goal: (0,0) gem, (80,0) ice   -> next y = 64 + 48
            //   icon:      (0,112) icon-star-empty -> next y = 176 + 48
            //   (none):    (0,224) hero
            var expected = new[]
            {
                new SKPoint(0, 112),  // icon-star-empty
                new SKPoint(0, 224),  // hero
                new SKPoint(80, 0),   // icon-goal-ice
                new SKPoint(0, 0),    // icon-goal-gem
            };
            for (var i = 0; i < all.Length; i++)
            {
                var actual = all[i].GetBoundingBox().Location;
                Assert.True(actual == expected[i],
                    $"artboard {i} ({all[i].Name}) at {actual}, expected {expected[i]}");
            }

            Assert.True(h.Operations.UndoOperationsCount == undoBefore + 1,
                $"undo count {undoBefore} -> {h.Operations.UndoOperationsCount}, expected +1");
        });

        t.Check("undo restores the pre-arrange layout", () =>
        {
            h.Operations.Undo();
            var xs = h.Artboards.Select(a => a.GetBoundingBox().Left).ToArray();
            Assert.True(xs.SequenceEqual([0f, 80f, 160f, 240f]), $"x positions = [{string.Join(", ", xs)}]");
        });

        // --- Canvas-edit sub-modes (Resize / Crop) ------------------------------------------------
        t.Check("cancelling a Resize session leaves the artboard untouched", () =>
        {
            var sprite = h.Artboards[0];
            var size = sprite.Size;
            h.SelectNodes(sprite);

            h.CanvasEdit.Begin(sprite, ArtboardObjectEditMode.Resize);
            Assert.True(h.CanvasEdit.IsActive, "session did not start");
            Assert.True(h.CanvasEdit.Mode == ArtboardObjectEditMode.Resize, $"mode = {h.CanvasEdit.Mode}");

            h.CanvasEdit.CancelMode();
            Assert.True(!h.CanvasEdit.IsActive, "session did not end");
            Assert.True(sprite.Size == size, $"size changed to {sprite.Size} on cancel (was {size})");
        });

        t.Check("a Resize session keeps the General context (it is a sub-mode, not a mode switch)", () =>
        {
            var sprite = h.Artboards[0];
            h.CanvasEdit.Begin(sprite, ArtboardObjectEditMode.Crop);
            Assert.True(h.AppState.CurrentProject.CurrentContextType == EditContextType.General,
                $"context = {h.AppState.CurrentProject.CurrentContextType}");
            h.CanvasEdit.CancelMode();
        });

        t.Check("confirming an untouched frame is a no-op (nothing to apply)", () =>
        {
            var sprite = h.Artboards[0];
            var size = sprite.Size;
            var undoBefore = h.Operations.UndoOperationsCount;

            h.CanvasEdit.Begin(sprite, ArtboardObjectEditMode.Crop);
            h.CanvasEdit.ConfirmMode();

            Assert.True(!h.CanvasEdit.IsActive, "session did not end");
            Assert.True(sprite.Size == size, $"size changed to {sprite.Size}");
            Assert.True(h.Operations.UndoOperationsCount == undoBefore,
                $"undo count {undoBefore} -> {h.Operations.UndoOperationsCount}, expected no new operation");
        });

        // --- Live result preview -------------------------------------------------------------------
        // Dragging the handles used to move the outline and nothing else, so neither sub-mode showed what
        // it was about to do. Resize now hands the artboard's on-screen pixels to the overlay (a stretched
        // snapshot) and render-suppresses the real node; Crop keeps the node painting and dims what will be
        // trimmed. Both stay preview-only: the document changes on Apply, never during the drag.
        t.Check("a Resize session previews the scaled artboard in place of the real node", () =>
        {
            var sprite = h.Artboards[0];
            var bounds = sprite.GetBoundingBox();
            h.SelectNodes(sprite);
            h.CanvasEdit.Begin(sprite, ArtboardObjectEditMode.Resize);

            Assert.True(sprite.IsRenderSuppressed,
                "the artboard still paints itself — the stretched preview would be drawn on top of it");
            Assert.True(h.CanvasEdit.FrameRect == bounds,
                $"frame started at {h.CanvasEdit.FrameRect}, expected the artboard bounds {bounds}");

            // Drag the bottom-right corner handle +32,+32 (world units at the pinned 1:1 zoom).
            h.PressWorld(bounds.Right, bounds.Bottom);
            h.MoveWorld(bounds.Right + 16, bounds.Bottom + 16, pressed: true);
            h.MoveWorld(bounds.Right + 32, bounds.Bottom + 32, pressed: true);
            h.ReleaseWorld(bounds.Right + 32, bounds.Bottom + 32);

            Assert.True(h.CanvasEdit.FrameRect.Width == bounds.Width + 32
                        && h.CanvasEdit.FrameRect.Height == bounds.Height + 32,
                $"frame is {h.CanvasEdit.FrameRect.Size}, expected {bounds.Width + 32}x{bounds.Height + 32}");
            Assert.True(sprite.Size == bounds.Size,
                $"the drag changed the canvas ({sprite.Size}) — it must stay a preview until Apply");
        });

        t.Check("applying the Resize scales the canvas and hands rendering back to the artboard", () =>
        {
            var sprite = h.Artboards[0];
            var expected = h.CanvasEdit.FrameRect.Size;
            h.CanvasEdit.ConfirmMode();

            Assert.True(sprite.Size == expected, $"size = {sprite.Size}, expected {expected}");
            Assert.True(!sprite.IsRenderSuppressed,
                "render suppression outlived the session — the artboard would never paint again");
        });

        // The object frame is drawn from NodesSelection.Frame — a node kept across Invalidate() calls so a
        // rotation survives them — so an applied Resize/Crop used to leave it framing the pre-edit canvas.
        t.Check("the object selection frame follows the artboard after an applied Resize", () =>
        {
            var sprite = h.Artboards[0];
            Assert.True(h.ObjectFrameBounds.Size == sprite.Size,
                $"frame is {h.ObjectFrameBounds.Size}, artboard is {sprite.Size}");
        });

        t.Check("undo restores the pre-resize canvas and the frame follows it back", () =>
        {
            var sprite = h.Artboards[0];
            h.Operations.Undo();

            Assert.True(sprite.Size.Width == 64 && sprite.Size.Height == 64,
                $"size after undo = {sprite.Size}, expected 64x64");
            Assert.True(h.ObjectFrameBounds.Size == sprite.Size,
                $"frame after undo is {h.ObjectFrameBounds.Size}, expected {sprite.Size}");
        });

        t.Check("a Crop session keeps the artboard painting itself (shield only, no stand-in)", () =>
        {
            var sprite = h.Artboards[0];
            h.CanvasEdit.Begin(sprite, ArtboardObjectEditMode.Crop);

            Assert.True(!sprite.IsRenderSuppressed,
                "Crop suppressed the artboard — the dimmed content it trims must stay visible");

            h.CanvasEdit.CancelMode();
            Assert.True(!sprite.IsRenderSuppressed, "cancel left the artboard suppressed");
        });

        t.Check("applying a Crop resizes the canvas and the frame follows", () =>
        {
            var sprite = h.Artboards[0];
            var bounds = sprite.GetBoundingBox();
            h.SelectNodes(sprite);
            h.CanvasEdit.Begin(sprite, ArtboardObjectEditMode.Crop);

            // Pull the right edge 24px in: crop keeps pixel scale, so the canvas just gets narrower.
            h.PressWorld(bounds.Right, bounds.MidY);
            h.MoveWorld(bounds.Right - 12, bounds.MidY, pressed: true);
            h.MoveWorld(bounds.Right - 24, bounds.MidY, pressed: true);
            h.ReleaseWorld(bounds.Right - 24, bounds.MidY);
            h.CanvasEdit.ConfirmMode();

            Assert.True(sprite.Size.Width == bounds.Width - 24 && sprite.Size.Height == bounds.Height,
                $"size = {sprite.Size}, expected {bounds.Width - 24}x{bounds.Height}");
            Assert.True(h.ObjectFrameBounds.Size == sprite.Size,
                $"frame is {h.ObjectFrameBounds.Size}, artboard is {sprite.Size}");

            h.Operations.Undo();
        });

        // --- Action bar visibility ----------------------------------------------------------------
        // The General action bar is gated on the top bar's Tools toggle, exactly like the Sprite
        // context's ActionsBarView (MainViewModel.ShowSpriteExtraTools). The view-model state is plain
        // (no Avalonia types), so the gate is assertable headless.
        t.Check("the General action bar follows the top bar's Tools toggle", () =>
        {
            h.AppState.CurrentProject.CurrentContextType = EditContextType.General;
            h.AppState.UiState.ShowExtraTools = true;

            var bar = new ObjectActionsBarView.State(h.AppState,
                h.Services.GetRequiredService<IMessenger>(), h.Commands, h.CanvasEdit);
            Assert.True(bar.IsVisible, "bar is hidden with Tools on in the General context");

            h.AppState.UiState.ShowExtraTools = false;
            Assert.True(!bar.IsVisible, "bar is still visible with Tools off");

            h.AppState.UiState.ShowExtraTools = true;
            Assert.True(bar.IsVisible, "bar did not come back when Tools was switched on again");
        });

        // Leave the safe default so the command sweep keeps declining destructive prompts.
        h.Dialogs.YesNoAnswer = false;
    }

    // --- Scenario 8: curated safe-sweep over ALL commands -------------------------------------------
    // ExecuteCommandAsync only checks the command's _canExecute flag, NOT its EditContextType, so a
    // blind "run everything" would fire data-loss / modal commands out of context. We skip a curated
    // denylist and run the rest, catching exceptions. Context-dependent commands are given the state
    // they need first: Sprite-context commands get a full-canvas pixel selection; General (object)
    // commands get a scene-node selection (and run last, since selecting a node changes edit state).
    // A thrown command is a FINDING to review; findings are reported but do NOT change the exit code
    // (the assertion scenarios above do that). Runs LAST because it mutates state unpredictably.
    static readonly string[] SkipPrefixes =
    [
        "File.",              // New / Open / Save / Import / Export / Exit — data loss or file dialogs
        "Crash.",             // diagnostics
        "Global.",            // DEBUG-only developer commands (3d/full mode switch, string export)
    ];
    static readonly string[] SkipExact =
    [
        "Sprite.Animation.TogglePlay",   // starts a playback loop (background timer)
        "Edit.Import",                   // needs a real file picker (Avalonia StorageProvider)
        "Window.RateAppCommand",         // store review — no-op headless (no IReviewService); real heads open the store/browser
        "Window.CloseRatePromptCommand", // store review — no-op headless (no IReviewService)
    ];

    // Commands that genuinely error without a pixel selection (rather than gracefully no-op'ing);
    // they get a full-canvas selection set up before running.
    static readonly HashSet<string> NeedsPixelSelection = ["Sprite.Edit.CropPixels"];

    // --- Eyedropper "switch back to previous tool" option (#215) -----------------------------------
    static void EyedropperReturnScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Eyedropper return-to-previous-tool scenario ===");

        h.NewProject(64);
        h.AppState.CurrentProject.CurrentContextType = EditContextType.Sprite;
        h.SetView(1);

        // A known pixel to pick from, so we can also prove the pick itself still happens.
        h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
        h.SetColor(SKColors.Red);
        h.DrawPixel(10, 10);
        h.SetColor(SKColors.Blue);

        var tools = h.AppState.ToolsState;
        var brushKey = nameof(Pix2d.Plugins.Drawing.Tools.BrushTool);
        var eyedropperKey = nameof(Pix2d.Plugins.Drawing.Tools.EyedropperTool);

        t.Check("switching tools records the outgoing one as PreviousToolKey", () =>
        {
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.EyedropperTool>();
            Assert.True(tools.PreviousToolKey == brushKey,
                $"PreviousToolKey = '{tools.PreviousToolKey}', expected '{brushKey}'");
        });

        t.Check("with the option off the eyedropper stays active after a pick", () =>
        {
            h.AppState.IsReturnToPreviousToolAfterColorPickEnabled = false;
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.EyedropperTool>();

            h.ClickWorld(10.5f, 10.5f);

            Assert.True(tools.CurrentToolKey == eyedropperKey,
                $"tool = '{tools.CurrentToolKey}', expected the eyedropper to stay active");
        });

        t.Check("with the option on the eyedropper hands back to the previous tool — and still picks", () =>
        {
            h.AppState.IsReturnToPreviousToolAfterColorPickEnabled = true;
            h.SetColor(SKColors.Blue);
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.EyedropperTool>();

            h.ClickWorld(10.5f, 10.5f);

            Assert.True(tools.CurrentToolKey == brushKey,
                $"tool = '{tools.CurrentToolKey}', expected a return to '{brushKey}'");

            var picked = h.AppState.SpriteEditorState.CurrentColor;
            Assert.True(picked.Red == 255 && picked.Green == 0 && picked.Blue == 0,
                $"the color was not picked before the hand-off: {picked}");
        });

        t.Check("the option is backed by an AppSettings property (survives a restart)", () =>
        {
            var settings = h.Services.GetRequiredService<ISettingsService>();
            settings.Set(nameof(AppState.IsReturnToPreviousToolAfterColorPickEnabled), true);

            // A FRESH service reading the real file: without the AppSettings property, Set is a logged
            // no-op and the toggle would silently reset on every launch.
            var reader = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            Assert.True(reader.TryGet<bool>(nameof(AppState.IsReturnToPreviousToolAfterColorPickEnabled), out var stored) && stored,
                "the toggle was not persisted");
        });

        h.AppState.IsReturnToPreviousToolAfterColorPickEnabled = false;
    }

    // --- Custom grid line color / opacity (#223) ---------------------------------------------------
    static void GridAppearanceScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Grid appearance scenario ===");

        h.NewProject(64);
        // Force-construct the service that owns the AppState -> scene push; its watchers are wired in
        // the ctor, so resolving it after the state change would miss the notification.
        var snapping = h.Services.GetRequiredService<ISnappingService>();
        var scene = h.Services.GetRequiredService<ISceneService>();

        // r=0x80 g=0xFF b=0x40 a=0xB0 — a semi-transparent green, nothing like the default gray.
        var custom = new SKColor(0x80, 0xFF, 0x40, 0xB0);

        t.Check("changing AppState.GridColor reaches every drawing container's grid", () =>
        {
            h.AppState.GridColor = custom;

            var containers = scene.GetCurrentSceneContainers<DrawingContainerBaseNode>();
            Assert.True(containers is { Count: > 0 }, "no drawing containers in the scene");
            foreach (var c in containers!)
                Assert.True(c.GridColor == custom, $"container '{c.Name}' grid color = {c.GridColor}, expected {custom}");
        });

        t.Check("a container created afterwards starts with the chosen color, not the default gray", () =>
        {
            Assert.True(GridDefaults.CurrentColor == custom,
                $"GridDefaults.CurrentColor = {GridDefaults.CurrentColor}, expected {custom}");

            // The grid node is built in DrawingContainerBaseNode's ctor, before any watcher can reach it.
            var fresh = new Pix2dSprite();
            Assert.True(fresh.GridColor == custom, $"new container grid color = {fresh.GridColor}");
        });

        t.Check("the color round-trips through its persisted #AARRGGBB form", () =>
        {
            var text = GridDefaults.FormatColor(custom);
            Assert.True(text == "#B080FF40", $"formatted as '{text}', expected #AARRGGBB");
            Assert.True(GridDefaults.ParseColor(text) == custom, "parse(format(x)) != x");
            Assert.True(GridDefaults.ParseColor(null) == GridDefaults.Color, "null should fall back to the default");
            Assert.True(GridDefaults.ParseColor("not a color") == GridDefaults.Color, "garbage should fall back to the default");
        });

        t.Check("the color is backed by an AppSettings property (survives a restart)", () =>
        {
            var settings = h.Services.GetRequiredService<ISettingsService>();
            settings.Set(nameof(AppState.GridColor), GridDefaults.FormatColor(custom));

            var reader = new SettingsService(h.Services.GetRequiredService<IPlatformStuffService>());
            Assert.True(reader.TryGet<string>(nameof(AppState.GridColor), out var stored)
                        && GridDefaults.ParseColor(stored) == custom,
                $"the grid color was not persisted (read back '{stored}')");
        });

        // The grid lives on the artboard's adorner layer, which is painted in the artboard's PARENT space
        // with the layer's own Position as the offset. Two things used to break that: the layer was created
        // in DrawingContainerBaseNode's ctor (before the node had a position or a parent, so it kept (0,0)),
        // and GridNode drew its WORLD bounding box onto a canvas that was already in its own space. So an
        // off-origin artboard's grid landed at double its offset, or at the scene origin. Render for real
        // and look at where the lines actually are.
        t.Check("an off-origin artboard's grid is drawn ON that artboard, not at the scene origin", () =>
        {
            h.NewProject(64);
            h.Exec("Sprite.Edit.AddArtboard");
            var boards = h.Artboards;
            Assert.True(boards.Length == 2, $"expected 2 artboards, got {boards.Length}");

            // An unmistakable offset, and a grid color nothing else in the scene uses.
            var second = boards[1];
            second.Position = new SKPoint(200, 0);
            h.AppState.GridColor = new SKColor(255, 0, 255);
            h.AppState.CurrentProject.ViewPortState.GridSpacing = new SKSize(8, 8);
            h.AppState.CurrentProject.ViewPortState.ShowGrid = true;
            h.SetView(1, 0, 0);

            var ink = RenderSceneAndFindColor(h, new SKColor(255, 0, 255));
            var boxes = boards.Select(b => b.GetBoundingBox()).ToArray();
            Console.WriteLine($"  [diag] grid pixels: {ink.Count}, artboards: {string.Join(", ", boxes)}");

            // Rasterization puts a line's ink a hair outside the mathematical rect, so allow 1px of slack
            // when deciding "inside" — the failure this guards against is off by 200 world units, not by 1.
            int OnBoard(SKRect b) => ink.Count(p => p.X >= b.Left - 1 && p.X <= b.Right + 1
                                                    && p.Y >= b.Top - 1 && p.Y <= b.Bottom + 1);

            var onFirst = OnBoard(boxes[0]);
            var onSecond = OnBoard(boxes[1]);
            var stray = ink.Where(p => boxes.All(b => !(p.X >= b.Left - 1 && p.X <= b.Right + 1
                                                        && p.Y >= b.Top - 1 && p.Y <= b.Bottom + 1))).ToArray();
            Console.WriteLine($"  [diag] on artboard 1: {onFirst}, on artboard 2 (offset): {onSecond}, stray: {stray.Length}");

            Assert.True(onSecond > 0,
                $"the artboard at x={boxes[1].Left} has no grid on it — its grid is drawn somewhere else");
            Assert.True(onFirst > 0, "the artboard at the origin has no grid on it");
            Assert.True(stray.Length == 0,
                $"{stray.Length} grid pixels landed off every artboard, first at {stray.FirstOrDefault()}");
        });

        h.AppState.CurrentProject.ViewPortState.ShowGrid = false;
        h.AppState.GridColor = GridDefaults.Color;
    }

    // --- Fill-tool opacity --------------------------------------------------------------------------
    static void FillOpacityScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Fill opacity scenario ===");

        h.NewProject(64);
        const int px = 12, py = 12;

        // The tool is a thin percent facade over IDrawingLayer.FillOpacity, which is what the pointer
        // pipeline reads. Build one directly (the activated instance isn't exposed) and check the mapping.
        var tool = ActivatorUtilities.CreateInstance<Pix2d.Plugins.Drawing.Tools.FillTool>(h.Services);
        t.Check("FillTool.Opacity maps percent onto the drawing layer's 0..1 fill opacity", () =>
        {
            tool.Opacity = 50;
            Assert.True(Math.Abs(h.DrawingLayer.FillOpacity - 0.5f) < 0.001f,
                $"FillOpacity = {h.DrawingLayer.FillOpacity}, expected 0.5");
            Assert.True(Math.Abs(tool.Opacity - 50) < 0.001, $"Opacity read back as {tool.Opacity}, expected 50");

            tool.Opacity = 500;
            Assert.True(h.DrawingLayer.FillOpacity == 1f, $"out-of-range input was not clamped: {h.DrawingLayer.FillOpacity}");
            tool.Opacity = -10;
            Assert.True(h.DrawingLayer.FillOpacity == 0f, $"out-of-range input was not clamped: {h.DrawingLayer.FillOpacity}");
        });

        // NewProject re-activates the default (brush) tool, so every case below re-arms the fill tool
        // *after* creating its canvas — otherwise DrawPixel quietly paints a single brush dab instead.
        void FreshFillCanvas(float opacity)
        {
            h.NewProject(64);
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.FillTool>();
            h.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Fill);
            h.DrawingLayer.FillOpacity = opacity;
        }

        t.Check("a full-strength fill still paints the flat opaque color", () =>
        {
            FreshFillCanvas(1f);
            h.SetColor(SKColors.Red);
            h.DrawPixel(px, py);

            var c = h.GetPixel(px, py);
            Assert.True(c.Alpha == 255 && c.Red == 255 && c.Green == 0 && c.Blue == 0,
                $"expected opaque red at ({px},{py}), got {c}");
            var corner = h.GetPixel(0, 0);
            Assert.True(corner.Alpha == 255 && corner.Red == 255,
                $"the fill should have flooded the whole empty canvas, but (0,0) is {corner}");
        });

        t.Check("a 50% fill on an empty canvas lands at half alpha", () =>
        {
            FreshFillCanvas(0.5f);
            h.SetColor(SKColors.Red);
            h.DrawPixel(px, py);

            var c = h.GetPixel(px, py);
            Assert.True(Math.Abs(c.Alpha - 128) <= 2, $"expected alpha ~128 at ({px},{py}), got {c}");
            Assert.True(c.Red >= 250 && c.Green == 0 && c.Blue == 0, $"expected red at ({px},{py}), got {c}");
        });

        // The check that catches an un-premultiplied write into the Premul working bitmap: with straight
        // RGBA bytes Skia reads the color channels as already-multiplied, and the red comes out at full
        // strength (255) instead of blending down to ~127 against the blue underneath.
        t.Check("a 50% fill over an opaque color blends instead of replacing", () =>
        {
            FreshFillCanvas(1f);
            h.SetColor(SKColors.Blue);
            h.DrawPixel(px, py);

            h.DrawingLayer.FillOpacity = 0.5f;
            h.SetColor(SKColors.Red);
            h.DrawPixel(px, py);

            var c = h.GetPixel(px, py);
            Console.WriteLine($"  [diag] 50% red over opaque blue = {c}");
            Assert.True(c.Alpha == 255, $"the result should stay opaque, got {c}");
            Assert.True(Math.Abs(c.Red - 127) <= 4 && Math.Abs(c.Blue - 128) <= 4,
                $"expected a ~50/50 red/blue blend at ({px},{py}), got {c}");
        });

        t.Check("a 50% fill in erase mode removes half the alpha", () =>
        {
            FreshFillCanvas(1f);
            h.SetColor(SKColors.Red);
            h.DrawPixel(px, py);

            h.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.FillErase);
            h.DrawingLayer.FillOpacity = 0.5f;
            h.DrawPixel(px, py);

            var c = h.GetPixel(px, py);
            Assert.True(Math.Abs(c.Alpha - 127) <= 2, $"expected alpha ~127 at ({px},{py}) after a half erase, got {c}");
        });

        t.Check("a zero-opacity fill is a no-op and pushes no undo step", () =>
        {
            FreshFillCanvas(0f);
            h.SetColor(SKColors.Red);

            var undoBefore = h.Operations.UndoOperationsCount;
            h.DrawPixel(px, py);

            Assert.True(h.GetPixel(px, py).Alpha == 0, $"expected an untouched pixel, got {h.GetPixel(px, py)}");
            Assert.True(h.Operations.UndoOperationsCount == undoBefore,
                $"undo count {undoBefore} -> {h.Operations.UndoOperationsCount}, expected no change");
        });

        // Leave the shared drawing layer as the rest of the suite expects it.
        h.DrawingLayer.FillOpacity = 1f;
        h.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.Draw);
    }

    /// <summary>
    /// Renders the live scene (adorners included — the grid is one) through the real renderer and returns
    /// every pixel of <paramref name="color"/> it finds, mapped back to WORLD coordinates. This is the only
    /// way to catch a node that paints in the wrong coordinate space: every state-level assertion passes
    /// while the ink lands somewhere else entirely.
    /// </summary>
    static List<SKPoint> RenderSceneAndFindColor(HeadlessHarness h, SKColor color)
    {
        const int size = 512;
        var vp = h.Services.GetRequiredService<IViewPortService>().ViewPort!;
        var root = SKApp.SceneManager.GetRootNode();

        using var surface = SKSurface.Create(new SKImageInfo(size, size, Pix2DAppSettings.ColorType, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        SKNodeRenderer.Render(root, new RenderContext(surface.Canvas, vp));
        surface.Canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);

        var found = new List<SKPoint>();
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.Red != color.Red || c.Green != color.Green || c.Blue != color.Blue || c.Alpha == 0)
                continue;

            found.Add(vp.ViewportToWorld(new SKPoint(x, y)));
        }

        return found;
    }

    // --- Shape tools rasterize with the currently selected brush preset ---------------------------
    // Line/Rect/Oval/Triangle stamp `IDrawingLayer.Brush` exactly like a freehand stroke does, so every
    // preset — including a soft marker/spray and a captured image stamp — has to behave the same way in
    // a shape as it does under the pencil: its own size, its own opacity, its own spacing phase.
    static void ShapeBrushScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Shape tools with custom brushes ===");

        var state = h.AppState.SpriteEditorState;

        // A fresh canvas with `TTool` armed and `brush` selected. NewProject re-activates the default
        // (pencil) tool, so the shape tool must be armed *after* the canvas exists.
        void Arm<TTool>(IPixelBrush brush, float scale, float opacity, float spacing = 1f)
            where TTool : Pix2d.Abstract.Tools.ITool
        {
            h.NewProject(64);
            h.ActivateTool<TTool>();
            h.SetColor(SKColors.Red);
            state.CurrentBrushSettings = new BrushSettings
            {
                Brush = brush,
                Scale = scale,
                Opacity = opacity,
                Spacing = spacing
            };
        }

        // Sprite-local pixel (x, y) is world (x + 0.5, y + 0.5).
        void DragPixels(int x0, int y0, int x1, int y1)
            => h.DragWorld(x0 + 0.5f, y0 + 0.5f, x1 + 0.5f, y1 + 0.5f);

        int ColumnHeight(int x) => h.NonEmptyPixels().Count(p => p.X == x);

        (int L, int T, int R, int B) Painted()
        {
            var pts = h.NonEmptyPixels().ToArray();
            return pts.Length == 0
                ? (0, 0, -1, -1)
                : (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
        }

        t.Check("a 1px brush still draws a hairline (baseline)", () =>
        {
            Arm<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(new SquareSolidBrush(), 1, 1f);
            DragPixels(10, 20, 40, 20);

            Assert.True(h.GetPixel(10, 20).Alpha > 0, "the line does not start where the pointer went down");
            Assert.True(h.GetPixel(40, 20).Alpha > 0, "the line does not reach the release point");
            Assert.True(ColumnHeight(25) == 1, $"expected a 1px-thick line, column 25 is {ColumnHeight(25)}px");
        });

        t.Check("a line is as thick as the selected brush", () =>
        {
            Arm<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(new SquareSolidBrush(), 5, 1f);
            DragPixels(10, 20, 40, 20);

            Assert.True(ColumnHeight(25) == 5, $"expected a 5px-thick line, column 25 is {ColumnHeight(25)}px");
        });

        t.Check("a marker line honours the brush opacity", () =>
        {
            // Marker is an even-opacity brush: the dabs union into the stroke buffer at full strength and
            // the whole stroke lands once at the brush opacity. Shapes go through the ExternalDraw mode,
            // which used to skip that composite entirely and paint a solid, fully opaque line.
            Arm<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(new MarkerBrush(), 8, 0.5f);
            DragPixels(10, 32, 50, 32);

            var c = h.GetPixel(30, 32);
            Console.WriteLine($"  [diag] marker line core = {c}");
            Assert.True(c.Alpha > 0, "the marker line painted nothing");
            Assert.True(Math.Abs(c.Alpha - 128) <= 12, $"expected ~50% alpha along a 0.5-opacity marker line, got {c}");
        });

        t.Check("an oval stamps the brush at its own size", () =>
        {
            // StrokeRenderer.DrawEllipse used to hand brush.Size to IPixelBrush.Draw's `pressure`
            // parameter, which multiplies the stamp scale — a size-5 brush stamped at 25px, off-center.
            Arm<Pix2d.Plugins.Drawing.Tools.Shapes.PixelOvalTool>(new SquareSolidBrush(), 5, 1f);
            DragPixels(16, 16, 44, 44);

            var (l, top, r, b) = Painted();
            Console.WriteLine($"  [diag] oval painted bounds = ({l},{top})-({r},{b})");
            Assert.True(r >= 0, "the oval painted nothing");
            // The outline spans 16..44; a 5px stamp centered on it may bleed 2-3px either way, no more.
            Assert.True(l >= 13 && top >= 13 && r <= 47 && b <= 47,
                $"the oval's stamps are oversized: bounds ({l},{top})-({r},{b}), expected within (13,13)-(47,47)");
        });

        t.Check("an image-stamp brush paints its captured pixels along a shape", () =>
        {
            using var stamp = new SKBitmap(3, 3, SKColorType.Rgba8888, SKAlphaType.Premul);
            stamp.Erase(SKColors.Lime);

            Arm<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(
                new ImageStampBrush(stamp.Copy(), useOriginalColors: true), 3, 1f);
            DragPixels(12, 24, 40, 24);

            var c = h.GetPixel(26, 24);
            Assert.True(c.Alpha > 0, "the stamp brush painted nothing along the line");
            Assert.True(c.Green > 200 && c.Red < 60,
                $"expected the stamp's own green pixels (UseOriginalColors), got {c}");
            Assert.True(ColumnHeight(26) == 3, $"expected a 3px-thick stamped line, column 26 is {ColumnHeight(26)}px");
        });

        t.Check("a shape always starts at the point the pointer went down", () =>
        {
            // Dab spacing is stroke-local state on the brush. It used to survive from one shape to the
            // next (and from one preview redraw to the next), so a short shape drawn near the previous
            // one had its opening dabs swallowed — with a wide enough spacing, the whole shape vanished.
            Arm<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(new SquareSolidBrush(), 1, 1f, spacing: 5f);

            DragPixels(10, 20, 11, 20);
            Assert.True(h.GetPixel(10, 20).Alpha > 0, "the first short line did not paint its start point");

            DragPixels(12, 20, 13, 20);
            Assert.True(h.GetPixel(12, 20).Alpha > 0,
                "a short line drawn next to the previous one inherited its dab spacing and painted nothing");
        });

        // Every check above draws on the harness's default artboard, which sits at world (0, 0) — where the
        // layer-local→world transform is the identity and a double-mapped outline is indistinguishable from
        // a correct one. A second artboard has a non-zero offset and is the only way to catch that here.
        void ArmOnSecondArtboard<TTool>(float scale) where TTool : Pix2d.Abstract.Tools.ITool
        {
            Arm<TTool>(new SquareSolidBrush(), scale, 1f);
            h.Exec("Sprite.Edit.AddArtboard");
            h.ActivateTool<TTool>();   // AddArtboard re-targets the editor; re-arm so the drag is a shape
        }

        // Sprite-local (x, y) on the *active* artboard → world, via its own bounding box.
        void DragOnActive(int x0, int y0, int x1, int y1)
        {
            var b = h.ActiveSprite.GetBoundingBox();
            h.DragWorld(b.Left + x0 + 0.5f, b.Top + y0 + 0.5f, b.Left + x1 + 0.5f, b.Top + y1 + 0.5f);
        }

        t.Check("the second artboard really is offset (guards the checks below)", () =>
        {
            ArmOnSecondArtboard<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(1);
            Assert.True(h.ArtboardCount == 2, $"expected 2 artboards, got {h.ArtboardCount}");
            var b = h.ActiveSprite.GetBoundingBox();
            Assert.True(b.Left != 0 || b.Top != 0,
                $"the new artboard is at the world origin ({b.Left},{b.Top}) — the offset checks would be vacuous");

            // The offset only exercises the world/layer split if the drawing layer actually carries it in
            // its global transform — that transform is what a double-mapped outline gets shifted by. If it
            // is identity here, every check below passes whether or not the mapping is correct.
            var t2 = ((SKNode)h.DrawingLayer).GetGlobalTransform();
            Console.WriteLine($"  [diag] artboard world=({b.Left},{b.Top}) drawing-layer transform=({t2.TransX},{t2.TransY})");
            Assert.True(Math.Abs(t2.TransX - b.Left) < 0.01f && Math.Abs(t2.TransY - b.Top) < 0.01f,
                $"the drawing layer sits at ({t2.TransX},{t2.TransY}) but its artboard is at ({b.Left},{b.Top}) — "
                + "the harness is not modelling the offset, so the checks below prove nothing");
        });

        t.Check("a line draws on an artboard away from the world origin", () =>
        {
            // The shape builders hand over layer-local points; StrokeRenderer.DrawStroke used to map them
            // through the inverse global transform anyway, shifting the outline by the artboard's world
            // position — so on any artboard but the first, shapes silently painted nothing at all.
            ArmOnSecondArtboard<Pix2d.Plugins.Drawing.Tools.Shapes.PixelLineTool>(5);
            DragOnActive(10, 20, 40, 20);

            Assert.True(h.GetPixel(25, 20).Alpha > 0, "the line painted nothing on the offset artboard");
            Assert.True(ColumnHeight(25) == 5, $"expected a 5px-thick line, column 25 is {ColumnHeight(25)}px");
        });

        t.Check("a rectangle draws on an artboard away from the world origin", () =>
        {
            ArmOnSecondArtboard<Pix2d.Plugins.Drawing.Tools.Shapes.PixelRectangleTool>(1);
            DragOnActive(12, 12, 40, 40);

            Assert.True(h.GetPixel(12, 12).Alpha > 0 && h.GetPixel(40, 40).Alpha > 0,
                "the rectangle painted nothing on the offset artboard");
            Assert.True(h.GetPixel(26, 26).Alpha == 0, "the rectangle should be an outline, not filled");
        });

        t.Check("a triangle draws on an artboard away from the world origin", () =>
        {
            ArmOnSecondArtboard<Pix2d.Plugins.Drawing.Tools.PixelTriangleTool>(1);
            DragOnActive(12, 12, 40, 40);

            Assert.True(h.NonEmptyPixels().Any(), "the triangle painted nothing on the offset artboard");
            Assert.True(h.GetPixel(12, 40).Alpha > 0 && h.GetPixel(40, 40).Alpha > 0,
                "the triangle's baseline corners are missing");
        });

        t.Check("an oval still draws on an artboard away from the world origin", () =>
        {
            // DrawEllipse was the one shape rasterizer that already treated its input as layer-local, so it
            // worked here all along — this pins that the world/local split above did not flip it the other way.
            ArmOnSecondArtboard<Pix2d.Plugins.Drawing.Tools.Shapes.PixelOvalTool>(1);
            DragOnActive(12, 12, 40, 40);

            var (l, top, r, b) = Painted();
            Assert.True(r >= 0, "the oval painted nothing on the offset artboard");
            Assert.True(l >= 10 && top >= 10 && r <= 42 && b <= 42,
                $"the oval landed outside its drag bounds: ({l},{top})-({r},{b})");
        });
    }

    // --- Scenario 7bd: symmetry axes (#23, #214) -----------------------------------------------------
    // Two halves. The first drives SymmetryMath directly (pure, no harness needed) because the
    // interesting cases are geometric: a wide brush must stay ON the axis rather than drift by half its
    // size, a dab on the axis must not be stamped twice, and N axes must generate the dihedral group.
    // The second half draws through the real pipeline so the state -> IDrawingService -> drawing-layer
    // wiring is covered as well, and asserts the *count* of painted pixels — which is what separates the
    // new "X+Y = four images" from the old behaviour (a 180 degree rotation, i.e. two).
    static void SymmetryScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Symmetry scenario ===");

        var canvas = new SKSize(64, 64);
        var pixelBrushCenter = SKPointI.Empty; // a 1px brush anchors on the pixel itself

        SKPointI[] Images(SymmetrySettings s, SKPointI anchor, SKPointI brushCenter = default, int brushSize = 1)
        {
            var buffer = new List<SKPointI>();
            SymmetryMath.GetImageAnchors(SymmetryMath.BuildTransforms(s, canvas), anchor, brushCenter, brushSize, buffer);
            return buffer.ToArray();
        }

        t.Check("symmetry off produces no images", () =>
            Assert.True(Images(SymmetrySettings.Off, new SKPointI(10, 20)).Length == 0,
                "an off symmetry still mirrored the dab"));

        t.Check("one vertical axis mirrors X and leaves Y alone", () =>
        {
            var images = Images(SymmetrySettings.MirrorX(), new SKPointI(10, 20));
            Assert.True(images.Length == 1 && images[0] == new SKPointI(53, 20),
                $"expected [(53,20)], got [{string.Join(", ", images)}]");
        });

        t.Check("one horizontal axis mirrors Y and leaves X alone", () =>
        {
            var images = Images(SymmetrySettings.MirrorY(), new SKPointI(10, 20));
            Assert.True(images.Length == 1 && images[0] == new SKPointI(10, 43),
                $"expected [(10,43)], got [{string.Join(", ", images)}]");
        });

        // The discriminating one: pre-3.12 both toggles on produced a single image (the 180 degree
        // rotation), so a "symmetric" drawing was only symmetric about the centre point.
        t.Check("X+Y produces three images, not one", () =>
        {
            var images = Images(SymmetrySettings.MirrorBoth(), new SKPointI(10, 20));
            Assert.True(images.Length == 3, $"expected 3 images, got {images.Length}");
            Assert.True(images.Contains(new SKPointI(53, 20)), "missing the vertical-axis mirror");
            Assert.True(images.Contains(new SKPointI(10, 43)), "missing the horizontal-axis mirror");
            Assert.True(images.Contains(new SKPointI(53, 43)), "missing the diagonal (rotated) image");
        });

        // A 4px brush anchors at CenterPoint (1,1) and covers [anchor-1, anchor+3), so its footprint
        // centre sits on x = 32 when the anchor is 31. Reflecting the anchor instead of the footprint
        // would move it by the brush size and produce a spurious second stamp.
        t.Check("a wide brush centred on the axis is not stamped a second time", () =>
        {
            var images = Images(SymmetrySettings.MirrorX(), new SKPointI(31, 20), new SKPointI(1, 1), brushSize: 4);
            Assert.True(images.Length == 0, $"expected no extra stamp, got [{string.Join(", ", images)}]");
        });

        t.Check("a dab on a half-pixel axis is not double-stamped", () =>
        {
            var images = Images(SymmetrySettings.MirrorX(new SKPoint(32.5f, 32)), new SKPointI(32, 20));
            Assert.True(images.Length == 0, $"expected no extra stamp, got [{string.Join(", ", images)}]");
        });

        t.Check("a moved centre moves the mirror", () =>
        {
            var images = Images(SymmetrySettings.MirrorX(new SKPoint(16, 32)), new SKPointI(10, 20));
            Assert.True(images.Length == 1 && images[0] == new SKPointI(21, 20),
                $"expected [(21,20)], got [{string.Join(", ", images)}]");
        });

        t.Check("N axes generate 2N distinct images", () =>
        {
            for (var n = 1; n <= SymmetrySettings.MaxAxisCount; n++)
            {
                // (7, 11) off both axes and off the diagonals, so no image collapses onto another.
                var images = Images(new SymmetrySettings { IsEnabled = true, AxisCount = n, AngleDegrees = 7 },
                    new SKPointI(7, 11));
                Assert.True(images.Length == 2 * n - 1,
                    $"{n} axes produced {images.Length} extra images, expected {2 * n - 1}");
                Assert.True(images.Distinct().Count() == images.Length, $"{n} axes produced duplicate images");
            }
        });

        t.Check("the axis count is clamped into range", () =>
        {
            Assert.True(new SymmetrySettings { AxisCount = 0 }.AxisCount == SymmetrySettings.MinAxisCount,
                "0 axes was not clamped up");
            Assert.True(new SymmetrySettings { AxisCount = 999 }.AxisCount == SymmetrySettings.MaxAxisCount,
                "999 axes was not clamped down");
            Assert.True(SymmetrySettings.Off.AxisCount == SymmetrySettings.MinAxisCount,
                "the default settings value has an undrawable axis count");
        });

        t.Check("a moved centre is clamped into the canvas, an unset one follows it", () =>
        {
            var moved = SymmetrySettings.MirrorX(new SKPoint(500, -20)).GetCenter(canvas);
            Assert.True(moved == new SKPoint(64, 0), $"expected the centre clamped to (64,0), got {moved}");
            Assert.True(SymmetrySettings.MirrorX().GetCenter(new SKSize(32, 48)) == new SKPoint(16, 24),
                "an unset centre did not resolve to the middle of the canvas");
        });

        t.Check("the overlay's axis segment spans the canvas", () =>
        {
            Assert.True(SymmetryMath.TryGetAxisSegment(SymmetrySettings.MirrorX(), canvas, 0, out var a, out var b),
                "the vertical axis was reported as missing the canvas");
            var top = a.Y < b.Y ? a : b;
            var bottom = a.Y < b.Y ? b : a;
            Assert.True(top == new SKPoint(32, 0) && bottom == new SKPoint(32, 64),
                $"expected (32,0)-(32,64), got {a}-{b}");
        });

        // --- through the real drawing pipeline ---------------------------------------------------
        var drawing = h.Services.GetRequiredService<IDrawingService>();

        int PaintOnce(SymmetrySettings settings, int x, int y)
        {
            h.NewProject(64);
            // NewProject re-activates the default tool, so arm the brush after it.
            h.ActivateTool<Pix2d.Plugins.Drawing.Tools.BrushTool>();
            h.SetColor(SKColors.Red);
            drawing.SetSymmetry(settings);
            h.DrawPixel(x, y);
            return h.NonEmptyPixels().Count();
        }

        t.Check("SetSymmetry reaches both the app state and the drawing layer", () =>
        {
            h.NewProject(64);
            drawing.SetSymmetry(SymmetrySettings.MirrorBoth());
            Assert.True(h.AppState.SpriteEditorState.Symmetry.IsEnabled, "the app state did not record the symmetry");
            Assert.True(h.DrawingLayer.Symmetry.AxisCount == 2, "the drawing layer did not receive the settings");

            // A new project rebuilds the drawing target; a session setting has to survive that.
            h.NewProject(64);
            Assert.True(h.DrawingLayer.Symmetry.IsEnabled,
                "symmetry was lost when the drawing target was rebuilt");
        });

        t.Check("a stroke with symmetry off paints one pixel", () =>
        {
            var count = PaintOnce(SymmetrySettings.Off, 10, 20);
            Assert.True(count == 1, $"expected 1 painted pixel, got {count}");
        });

        t.Check("mirror X paints the dab and its mirror", () =>
        {
            var count = PaintOnce(SymmetrySettings.MirrorX(), 10, 20);
            Assert.True(count == 2, $"expected 2 painted pixels, got {count}");
            Assert.True(h.GetPixel(53, 20).Red == 255, "the mirrored pixel is not on the far side of the axis");
        });

        t.Check("X+Y paints four pixels", () =>
        {
            var count = PaintOnce(SymmetrySettings.MirrorBoth(), 10, 20);
            Assert.True(count == 4, $"expected 4 painted pixels, got {count}");
            Assert.True(h.GetPixel(53, 20).Red == 255 && h.GetPixel(10, 43).Red == 255 && h.GetPixel(53, 43).Red == 255,
                "the four-way symmetry did not paint all three mirrors");
        });

        t.Check("four radial axes paint eight pixels", () =>
        {
            var count = PaintOnce(new SymmetrySettings { IsEnabled = true, AxisCount = 4, AngleDegrees = 0 }, 10, 20);
            Assert.True(count == 8, $"expected 8 painted pixels, got {count}");
        });

        // The overlay's grip is a real hit target, so wherever it sits it eats presses. It is parked
        // outside the canvas for exactly this reason: the first cut put it on the intersection, which
        // defaults to the middle of the canvas, and this check painted 0 pixels instead of 2.
        t.Check("the axis grip does not swallow strokes near the symmetry centre", () =>
        {
            var count = PaintOnce(SymmetrySettings.MirrorX(), 32, 32);
            Assert.True(count >= 1, "a dab on the symmetry centre painted nothing — the grip ate the press");
        });

        t.Check("a moved centre moves the painted mirror", () =>
        {
            var count = PaintOnce(SymmetrySettings.MirrorX(new SKPoint(16, 32)), 10, 20);
            Assert.True(count == 2, $"expected 2 painted pixels, got {count}");
            Assert.True(h.GetPixel(21, 20).Red == 255, "the mirror did not follow the moved centre");
            Assert.True(h.GetPixel(53, 20).Alpha == 0, "the mirror was still painted about the canvas centre");
        });

        t.Check("SetSymmetryCenter(null) puts the axes back in the middle", () =>
        {
            drawing.SetSymmetry(SymmetrySettings.MirrorX(new SKPoint(16, 32)));
            drawing.SetSymmetryCenter(null);
            Assert.True(!h.AppState.SpriteEditorState.Symmetry.Center.HasValue, "the centre was not cleared");
            Assert.True(h.AppState.SpriteEditorState.Symmetry.IsEnabled, "clearing the centre also turned symmetry off");
        });

        // The grip sits on empty canvas background, where nothing else says "draggable" — the grab hand is
        // the whole affordance. Headlessly OnDraw never runs, so the node's world-per-pixel stays 1 and the
        // grip is 12 world units above the top of the axis.
        t.Check("the pointer over the grip asks for a grab hand", () =>
        {
            h.NewProject(64);
            drawing.SetSymmetry(SymmetrySettings.MirrorX());

            h.MoveWorld(32, -12, pressed: false);
            Assert.True(SKInput.Current.HoverCursor == SKCursorType.Hand,
                $"expected Hand over the grip, got {SKInput.Current.HoverCursor}");

            h.MoveWorld(20, 30, pressed: false);
            Assert.True(SKInput.Current.HoverCursor == SKCursorType.Default,
                $"expected Default over the artwork, got {SKInput.Current.HoverCursor}");

            drawing.SetSymmetry(SymmetrySettings.Off);
            h.MoveWorld(32, -12, pressed: false);
            Assert.True(SKInput.Current.HoverCursor == SKCursorType.Default,
                "the grip still claimed the cursor with symmetry off");
        });

        drawing.SetSymmetry(SymmetrySettings.Off);
        h.NewProject(64);
    }

    // --- Scenario 7bc: layer titles (#67) and "select the layer's opaque pixels" (#57) ---------------
    // Both are driven through the view-models the real UI uses (LayerOptionsView.State.Rename and
    // LayersView.State.SelectLayerPixels — plain ObservableObjects, no Avalonia types), so the wiring
    // between the panel, the sprite editor and the drawing layer is what gets exercised, not just the
    // services underneath.
    static void LayerTitleAndPixelMaskScenario(HeadlessHarness h, TestReport t)
    {
        Console.WriteLine("\n=== Layer title / select-opaque-pixels scenario ===");

        h.NewProject(32);
        h.SetColor(SKColors.Red);

        // A 3x3 block at (10,10) — small enough that a marquee covering it can't be confused with the
        // full-canvas marquee SelectAll would produce.
        for (var y = 10; y <= 12; y++)
            for (var x = 10; x <= 12; x++)
                h.DrawPixel(x, y);

        var editor = h.SpriteEditor;
        var maskLayer = editor.SelectedLayer!;
        var originalName = maskLayer.Name;
        var options = ActivatorUtilities.CreateInstance<LayerOptionsView.State>(h.Services);

        t.Check("rename: the layer options panel writes the new title", () =>
        {
            h.Dialogs.InputAnswer = "Outline";
            options.Rename();
            Assert.True(maskLayer.Name == "Outline", $"layer name is '{maskLayer.Name}', expected 'Outline'");
        });

        t.Check("rename: the prompt is seeded with the current title", () =>
            Assert.True(h.Dialogs.LastInputDefaultValue == originalName,
                $"prompt was seeded with '{h.Dialogs.LastInputDefaultValue}', expected '{originalName}'"));

        t.Check("rename: one undo step, and undo/redo moves the title back and forth", () =>
        {
            h.Exec("Edit.Undo");
            Assert.True(maskLayer.Name == originalName, $"after undo the name is '{maskLayer.Name}', expected '{originalName}'");
            h.Exec("Edit.Redo");
            Assert.True(maskLayer.Name == "Outline", $"after redo the name is '{maskLayer.Name}', expected 'Outline'");
        });

        t.Check("rename: a blank name is rejected and pushes no undo step", () =>
        {
            var undoBefore = h.UndoStackSize;
            h.Dialogs.InputAnswer = "   ";
            options.Rename();
            Assert.True(maskLayer.Name == "Outline", $"a blank name overwrote the title with '{maskLayer.Name}'");
            Assert.True(h.UndoStackSize == undoBefore, $"undo stack {undoBefore} -> {h.UndoStackSize}, expected no change");
        });

        t.Check("rename: dismissing the prompt leaves the title alone", () =>
        {
            h.Dialogs.InputAnswer = null;
            options.Rename();
            Assert.True(maskLayer.Name == "Outline", $"a dismissed prompt changed the title to '{maskLayer.Name}'");
        });

        t.Check("the tile shows a user-given title but not an auto-generated one", () =>
        {
            var tile = new LayerItemView.State(new LayerItemViewModel(maskLayer, editor),
                h.Services.GetRequiredService<IViewPortRefreshService>());
            Assert.True(tile.ShowNameStrip && tile.LayerName == "Outline",
                $"named layer: strip={tile.ShowNameStrip} text='{tile.LayerName}', expected the name to show");

            maskLayer.Name = originalName;   // back to the generated "Layer NNN"
            tile.SyncFromModel();
            Assert.True(!tile.ShowNameStrip && tile.LayerName.Length == 0,
                $"unnamed layer: strip={tile.ShowNameStrip} text='{tile.LayerName}', expected no caption");

            // Reorder renumbers layers, so "is this the name it would get right now" is the wrong test —
            // any "Layer <n>" counts as unnamed.
            maskLayer.Name = "Layer 042";
            tile.SyncFromModel();
            Assert.True(!tile.ShowNameStrip, "a generated name from another index was treated as a real title");

            maskLayer.Name = "Outline";
            tile.SyncFromModel();
        });

        // --- #57: the mask comes from the clicked layer, the target stays the active one -------------
        h.Exec("Sprite.Edit.AddLayer");
        var activeLayer = editor.SelectedLayer!;
        var layers = ActivatorUtilities.CreateInstance<LayersView.State>(h.Services);

        t.Check("ctrl+click on a thumbnail selects that layer's opaque pixels", () =>
        {
            layers.SelectLayerPixels(new LayerItemViewModel(maskLayer, editor));
            Assert.True(h.HasPixelSelection, "no marquee after the gesture");

            var b = h.PixelSelectionBounds;
            Assert.True(Math.Abs(b.Left - 10) <= 1 && Math.Abs(b.Top - 10) <= 1
                        && Math.Abs(b.Width - 3) <= 1 && Math.Abs(b.Height - 3) <= 1,
                $"marquee is {b}, expected the 3x3 block at (10,10) — a full-canvas rect means the mask was ignored");
        });

        t.Check("... taking the mask from another layer without stealing the active one", () =>
            Assert.True(ReferenceEquals(editor.SelectedLayer, activeLayer),
                "the gesture switched the active layer; it must only load the selection"));

        t.Check("... and hands the marquee to a selection tool, not the brush", () =>
        {
            // The gesture activates the rect-selection tool; DrawingService then applies the user's
            // auto-open-transform preference on top, exactly as it does for a hand-drawn marquee — so
            // either of the two is a correct landing spot, the brush is not.
            var tool = h.AppState.ToolsState.CurrentToolKey;
            Assert.True(tool is "PixelSelectRectTool" or "PixelTransformTool",
                $"active tool is '{tool}', expected a selection tool");
        });

        t.Check("an empty layer selects nothing (and drops the previous marquee)", () =>
        {
            layers.SelectLayerPixels(new LayerItemViewModel(activeLayer, editor));
            Assert.True(!h.HasPixelSelection, "an empty layer produced a marquee");
        });
    }

    // --- Scenario 7bd: the self-update release parser -----------------------------------------------
    // Pure function, no harness needed. It earns a scenario because the update check swallows every
    // exception by design, so a parsing bug here disables self-update on all portable desktop builds
    // and shows up nowhere but a log line.
    static void UpdateReleaseParsingScenario(TestReport t)
    {
        Console.WriteLine("\n=== Self-update release parsing scenario ===");

        // Shape of a real https://api.github.com/repos/gritsenko/Pix2d/releases/latest payload.
        const string json = """
        {
          "tag_name": "v3.11.4",
          "name": "Pix2D v3.11.4",
          "body": "  Fixes and improvements.  ",
          "html_url": "https://github.com/gritsenko/Pix2d/releases/tag/v3.11.4",
          "draft": false,
          "prerelease": false,
          "published_at": "2026-08-06T12:34:56Z",
          "assets": [ { "browser_download_url": "https://example.invalid/Pix2d_win.zip" } ]
        }
        """;

        t.Check("a real release payload parses (the ISO date must not throw)", () =>
        {
            var info = UpdateService.ParseRelease(json);
            Assert.True(info != null, "ParseRelease returned null for a valid release");
            Assert.True(info!.Version == new Version(3, 11, 4), $"version {info.Version}, expected 3.11.4");
            Assert.True(info.Name == "Pix2D v3.11.4", $"name '{info.Name}'");
            Assert.True(info.ReleaseNotes == "Fixes and improvements.", $"notes '{info.ReleaseNotes}'");
            Assert.True(info.PublishedAt == new DateTimeOffset(2026, 8, 6, 12, 34, 56, TimeSpan.Zero),
                $"published {info.PublishedAt:O}, expected 2026-08-06T12:34:56Z");
            Assert.True(info.DownloadUrl == "https://example.invalid/Pix2d_win.zip", $"asset '{info.DownloadUrl}'");
        });

        t.Check("drafts and pre-releases are not update candidates", () =>
        {
            Assert.True(UpdateService.ParseRelease(json.Replace("\"draft\": false", "\"draft\": true")) == null,
                "a draft was offered as an update");
            Assert.True(UpdateService.ParseRelease(json.Replace("\"prerelease\": false", "\"prerelease\": true")) == null,
                "a pre-release was offered as an update");
        });

        t.Check("a missing or unparseable date degrades instead of throwing", () =>
        {
            var info = UpdateService.ParseRelease(json.Replace("\"2026-08-06T12:34:56Z\"", "\"not a date\""));
            Assert.True(info != null && info.PublishedAt == DateTimeOffset.MinValue,
                "a bad published_at should leave the rest of the release usable");
        });

        t.Check("a release with no usable tag is ignored", () =>
            Assert.True(UpdateService.ParseRelease(json.Replace("\"v3.11.4\"", "\"nightly\"")) == null,
                "a non-version tag was accepted"));
    }

    /// <summary>
    /// Guards the grouping key derived for crashes the app only learns about on the next launch
    /// (native crash / ANR, via Android's ApplicationExitInfo). The failure mode this protects
    /// against is silent and expensive: if anything device-specific leaks into the key — the
    /// per-install path hash, a program counter, a symbol offset — every phone reports its own
    /// unique "issue" and a widespread crash looks like a hundred one-off events.
    /// </summary>
    static void NativeCrashSignatureScenario(TestReport t)
    {
        Console.WriteLine("\n=== Recovered native crash signature scenario ===");

        // A real tombstone from Google Play for com.pix2d.pix2dapp. Note the shape that matters:
        // frame #00 is inside stripped Skia and resolves to no symbol, #01 lands on the exported
        // C-API entry point, and #02 has no mapped module at all.
        const string tombstone = """
        signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x0
        [split_config.arm64_v8a.apk!libSkiaSharp.so] sk_surface_draw
        *** *** *** *** *** *** *** *** *** *** *** *** *** *** *** ***
        pid: 0, tid: 30120 >>> com.pix2d.pix2dapp <<<

        backtrace:
          #00  pc 0000000000322500  /data/app/~~IlliRmL7ISC6U7xZvSurqg==/com.pix2d.pix2dapp-FLHCWNbuXTw0Lky34ZJ4Aw==/split_config.arm64_v8a.apk!libSkiaSharp.so
          #01  pc 0000000000249eb4  /data/app/~~IlliRmL7ISC6U7xZvSurqg==/com.pix2d.pix2dapp-FLHCWNbuXTw0Lky34ZJ4Aw==/split_config.arm64_v8a.apk!libSkiaSharp.so (sk_surface_draw+28)
          #02  pc 0000000000009b48
        """;

        static ProcessExitDetails Exit(string trace, int status = 11, string reason = "CrashNative") =>
            new()
            {
                LikelyCrash = true,
                Reason = reason,
                Description = "Native crash",
                TimestampMs = 1_754_000_000_000,
                TraceText = trace,
                Status = status,
            };

        t.Check("a real tombstone yields a named, actionable signature", () =>
        {
            var s = NativeCrashSignature.Derive(Exit(tombstone));
            Assert.True(s.SignalName == "SIGSEGV", $"signal '{s.SignalName}'");
            Assert.True(s.Library == "libSkiaSharp.so", $"library '{s.Library}'");
            Assert.True(s.Symbol == "sk_surface_draw", $"symbol '{s.Symbol}'");
            Assert.True(s.Title == "Native crash SIGSEGV in libSkiaSharp.so: sk_surface_draw",
                $"title '{s.Title}'");
            Assert.True(s.FaultCode == "SEGV_MAPERR", $"fault code '{s.FaultCode}'");
        });

        t.Check("nothing device-specific reaches the signature", () =>
        {
            var s = NativeCrashSignature.Derive(Exit(tombstone));
            foreach (var leak in new[] { "IlliRmL7ISC6U7xZvSurqg", "FLHCWNbuXTw0Lky34ZJ4Aw", "/data/app", "0x", "+28", "322500" })
            {
                Assert.True(!s.Fingerprint.Contains(leak, StringComparison.OrdinalIgnoreCase),
                    $"fingerprint leaked '{leak}': {s.Fingerprint}");
                Assert.True(!s.Title.Contains(leak, StringComparison.OrdinalIgnoreCase),
                    $"title leaked '{leak}': {s.Title}");
            }
        });

        t.Check("the same fault on another device/build groups identically", () =>
        {
            // Different install hash, different load addresses, different symbol offset — i.e. exactly
            // what varies between two users hitting one bug.
            var other = tombstone
                .Replace("IlliRmL7ISC6U7xZvSurqg==", "ZZZZZZZZZZZZZZZZZZZZZZ==")
                .Replace("FLHCWNbuXTw0Lky34ZJ4Aw==", "QQQQQQQQQQQQQQQQQQQQQQ==")
                .Replace("0000000000322500", "00000000004a1c20")
                .Replace("0000000000249eb4", "00000000003b2118")
                .Replace("sk_surface_draw+28", "sk_surface_draw+64");

            Assert.True(NativeCrashSignature.Derive(Exit(tombstone)).Fingerprint
                        == NativeCrashSignature.Derive(Exit(other)).Fingerprint,
                "two reports of the same fault produced different fingerprints");
        });

        t.Check("the signal comes from the OS status, not the trace text", () =>
        {
            // No signal line at all: Status alone must still identify it.
            var s = NativeCrashSignature.Derive(Exit("backtrace:\n  #00  pc 001  /x/libSkiaSharp.so (foo+1)", status: 6));
            Assert.True(s.SignalName == "SIGABRT", $"signal '{s.SignalName}'");
        });

        t.Check("an abort keys on its abort message", () =>
        {
            const string abort = """
            signal 6 (SIGABRT), code -1 (SI_QUEUE), fault addr --------
            Abort message: 'assertion failed at line 1234 of file /tmp/mono/foo.c'
            backtrace:
              #00  pc 0000000000089abc  /apex/com.android.runtime/lib64/bionic/libc.so (abort+164)
            """;
            var s = NativeCrashSignature.Derive(Exit(abort, status: 6));
            Assert.True(s.Title.StartsWith("Native abort:", StringComparison.Ordinal), $"title '{s.Title}'");
            // The line number and path are run-specific and must be collapsed by the normalizer.
            Assert.True(!s.Fingerprint.Contains("1234"), $"fingerprint kept a line number: {s.Fingerprint}");
        });

        t.Check("a system-library-only crash still gets a signature", () =>
        {
            const string libcOnly = """
            signal 11 (SIGSEGV), code 2 (SEGV_ACCERR), fault addr 0x10
            backtrace:
              #00  pc 0000000000045678  /apex/com.android.runtime/lib64/bionic/libc.so (memcpy+72)
            """;
            var s = NativeCrashSignature.Derive(Exit(libcOnly));
            Assert.True(s.Library == "libc.so" && s.Fingerprint.Length > 0, $"fingerprint '{s.Fingerprint}'");
        });

        t.Check("an ANR is not run through the native parser", () =>
        {
            const string anr = """
            "main" prio=5 tid=1 Blocked
              at Pix2d.Services.ProjectService.SaveBlocking(ProjectService.cs:88)
              at Pix2d.Commands.FileCommands.Save(FileCommands.cs:12)
            """;
            var s = NativeCrashSignature.Derive(new ProcessExitDetails
            {
                LikelyCrash = true, Reason = "Anr", TimestampMs = 1, TraceText = anr, Status = 0,
            });
            Assert.True(s.SignalName == "ANR", $"signal '{s.SignalName}'");
            Assert.True(s.Fingerprint.StartsWith("anr|", StringComparison.Ordinal), $"fingerprint '{s.Fingerprint}'");
            Assert.True(s.Title.Contains("ProjectService"), $"title '{s.Title}'");
        });

        t.Check("a missing or garbage trace degrades instead of throwing", () =>
        {
            Assert.True(NativeCrashSignature.Derive(Exit(null!)).Fingerprint.Length > 0, "null trace");
            Assert.True(NativeCrashSignature.Derive(Exit("")).Fingerprint.Length > 0, "empty trace");
            Assert.True(NativeCrashSignature.Derive(Exit("  not a tombstone")).Fingerprint.Length > 0,
                "garbage trace");
        });

        t.Check("a low-memory SIGKILL is recognised so it is never forwarded as a crash", () =>
        {
            Assert.True(Exit(tombstone, status: 9, reason: "Signaled").IsLowMemoryKill,
                "SIGKILL must be flagged as a low-memory kill");
            Assert.True(!Exit(tombstone).IsLowMemoryKill, "SIGSEGV must not be flagged as a low-memory kill");
        });
    }

    static void SafeSweep(HeadlessHarness h)
    {
        Console.WriteLine("\n=== Safe-sweep over all commands (curated skiplist + context setup) ===");

        var all = h.Commands.GetCommands().ToArray();
        var runnable = all
            .Where(c => !(SkipPrefixes.Any(p => c.Name.StartsWith(p, StringComparison.Ordinal))
                          || SkipExact.Contains(c.Name)))
            .OrderBy(c => c.Name)
            .ToArray();

        var executed = 0;
        var findings = new List<string>();

        // Phase A — the bulk: everything except object/General and the isolated selection commands,
        // run in sprite-editing context with no special setup.
        foreach (var cmd in runnable.Where(c => c.EditContextType != EditContextType.General
                                                 && !NeedsPixelSelection.Contains(c.Name)))
            RunSwept(h, cmd, ref executed, findings, setup: () => { });

        // Phase B — object/General commands, each with a fresh scene-node selection.
        foreach (var cmd in runnable.Where(c => c.EditContextType == EditContextType.General))
            RunSwept(h, cmd, ref executed, findings, setup: h.EnsureNodeSelection);

        // Phase C — selection-dependent commands (e.g. Crop) run LAST, each isolated on a fresh
        // project with a full-canvas selection. Isolation matters: Crop resizes the sprite and leaves
        // a pending working-bitmap that would corrupt SelectedLayerIndex for any command run after it.
        foreach (var cmd in runnable.Where(c => NeedsPixelSelection.Contains(c.Name)))
            RunSwept(h, cmd, ref executed, findings, setup: () =>
            {
                h.NewProject();
                h.EnsurePixelSelection();
            });

        Console.WriteLine($"  executed {executed}, skipped {all.Length - runnable.Length}, findings {findings.Count}");
        foreach (var f in findings)
            Console.WriteLine($"  FINDING  {f}");
    }

    static void RunSwept(HeadlessHarness h, Pix2dCommand cmd, ref int executed, List<string> findings, Action setup)
    {
        try
        {
            setup();
            h.Exec(cmd.Name);
            executed++;
        }
        catch (Exception ex)
        {
            findings.Add($"{cmd.Name}: {ex.GetType().Name}: {ex.Message}");
            if (Environment.GetEnvironmentVariable("SWEEP_TRACE") == "1")
                Console.WriteLine(ex.StackTrace);
        }
    }
}

sealed class HarnessApp : Application
{
    public override void Initialize() => Styles.Add(new SimpleTheme());
}

static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

sealed class TestReport
{
    private int _passed, _failed;

    public void Check(string name, Action assertion)
    {
        try
        {
            assertion();
            _passed++;
            Console.WriteLine($"  PASS  {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL  {name}");
            Console.WriteLine($"        {ex.Message}");
        }
    }

    public int Summarize()
    {
        Console.WriteLine($"\n=== {_passed} passed, {_failed} failed ===");
        return _failed == 0 ? 0 : 1;
    }
}
