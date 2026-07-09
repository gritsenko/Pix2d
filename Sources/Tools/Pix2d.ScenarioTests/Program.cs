using Avalonia;
using Avalonia.Themes.Simple;
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
