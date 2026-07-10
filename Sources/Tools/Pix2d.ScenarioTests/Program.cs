using Avalonia;
using Avalonia.Themes.Simple;
using Pix2d.Abstract;
using Pix2d.Primitives;
using Pix2d.ScenarioTests;
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
        ArtboardScenario(harness, t);
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
        "Window.RateAppCommand",         // store review — IReviewService is registered on Android only
        "Window.CloseRatePromptCommand", // store review — IReviewService is registered on Android only
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
