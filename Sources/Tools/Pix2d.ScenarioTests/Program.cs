using Avalonia;
using Avalonia.Themes.Simple;
using Microsoft.Extensions.DependencyInjection;
using Mvvm.Messaging;
using Newtonsoft.Json.Linq;
using Pix2d.Abstract;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.UI;
using Pix2d.Export;
using Pix2d.Export.Sheet;
using Pix2d.Plugins.PngFormat.Exporters;
using Pix2d.Export.Sheet.Metadata;
using Pix2d.Primitives;
using Pix2d.Primitives.ViewPort;
using Pix2d.ScenarioTests;
using SkiaNodes.Interactive;
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
        BatchExportScenario(harness, t);
        PrecisionScrollDetectorScenario(harness, t);
        PixelSelectionScenario(harness, t);
        GeneralContextObjectToolScenario(harness, t);
        GeneralContextObjectCommandsScenario(harness, t);
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
