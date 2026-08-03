using Avalonia;
using Avalonia.Themes.Simple;
using Microsoft.Extensions.DependencyInjection;
using Mvvm.Messaging;
using Newtonsoft.Json.Linq;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.Project;
using Pix2d.UI;
using Pix2d.Export;
using Pix2d.Export.Sheet;
using Pix2d.Plugins.PngFormat.Exporters;
using Pix2d.Export.Sheet.Metadata;
using Pix2d.Primitives;
using Pix2d.Primitives.Drawing;
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
        ArtboardScenario(harness, t);
        DegenerateCanvasScenario(harness, t);
        OversizedCanvasScenario(harness, t);
        MoveThumbSelectionChurnScenario(harness, t);
        ReadOnlyOverwriteScenario(harness, t);
        AnimationMetaScenario(harness, t);
        BrushPresetScenario(harness, t);
        BatchExportScenario(harness, t);
        PrecisionScrollDetectorScenario(harness, t);
        PixelSelectionScenario(harness, t);
        GeneralContextObjectToolScenario(harness, t);
        GeneralContextObjectCommandsScenario(harness, t);
        EyedropperReturnScenario(harness, t);
        GridAppearanceScenario(harness, t);
        FillOpacityScenario(harness, t);
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

            t.Check("OpenWriteAsync overwrites a read-only file", () =>
            {
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

                using (var stream = file.OpenWriteAsync().GetAwaiter().GetResult())
                    stream.Write([9, 9]);

                Assert.True(File.ReadAllBytes(path).Length == 2, "the stream did not truncate the file");
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
