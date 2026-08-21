using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Plugins.PngFormat.Exporters;
using Pix2d.State;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.ScenarioTests;

/// <summary>
/// A booted, headless Pix2d instance: real DI graph, a real (non-rendering) ViewPort, one project
/// loaded, ready to be driven programmatically. See the boot sequence in <see cref="Boot"/>.
///
/// Coordinate note: the ViewPort is created with Zoom = 1, ScaleFactor = 1, Pan = 0, so a viewport
/// point equals a world point, and sprite-local pixel (x, y) is the world point (x + 0.5, y + 0.5).
/// </summary>
public sealed class HeadlessHarness
{
    public IServiceProvider Services { get; }
    public AppState AppState { get; }
    public ICommandService Commands { get; }
    public IOperationService Operations { get; }

    private readonly IDrawingService _drawing;
    private readonly IToolService _tools;
    private readonly ISelectionService _selection;
    private readonly ViewPort _viewPort;
    private readonly SKInput _input = SKInput.Current;

    private HeadlessHarness(IServiceProvider services, ViewPort viewPort)
    {
        Services = services;
        _viewPort = viewPort;
        AppState = services.GetRequiredService<AppState>();
        Commands = services.GetRequiredService<ICommandService>();
        Operations = services.GetRequiredService<IOperationService>();
        _drawing = services.GetRequiredService<IDrawingService>();
        _tools = services.GetRequiredService<IToolService>();
        _selection = services.GetRequiredService<ISelectionService>();
    }

    /// <summary>
    /// Builds the container, wires the headless ViewPort + input, initializes the app, and creates a
    /// fresh <paramref name="size"/> project. Must be called on the STA thread that already ran
    /// <c>AppBuilder…SetupWithoutStarting()</c> (Program.Main does this).
    /// </summary>
    public static HeadlessHarness Boot(int size = 64)
    {
        var bootstrapper = new HeadlessBootstrapper();

        var serviceCollection = new ServiceCollection();
        bootstrapper.ConfigureServices(serviceCollection);
        var sp = bootstrapper.GetServiceProvider();

        // 1. A real ViewPort (plain SkiaNodes camera — no GL surface needed) at 1:1, no pan/zoom.
        var viewPort = new ViewPort(size, size) { ScaleFactor = 1 };
        viewPort.SetZoom(1);
        viewPort.SetPan(0, 0);

        // 2. Wire the static input router exactly like SkiaCanvas does, so Set* pointer calls route
        //    into the scene graph.
        SKInput.Current.RootNodeProvider = () => SKApp.SceneManager.GetRootNode();
        SKInput.Current.ViewPortProvider = () => viewPort;

        // 3. Initialize the viewport service (also calls AdornerLayer.Initialize, required before any
        //    Pix2dSprite is constructed). This sends ViewPortInitializedMessage now — before the app's
        //    own handler is registered in Initialize() below — so the startup auto-load path never
        //    fires and we create the project deterministically ourselves.
        sp.GetRequiredService<IViewPortService>().Initialize(viewPort);

        // 4. Bring the app up: command lists, SessionLogger, plugin init (tools + IDrawingService).
        bootstrapper.Initialize();

        // 5. Create the project (synchronous on the single-project path: no dialog, no await hit).
        sp.GetRequiredService<IProjectService>()
            .CreateNewProjectAsync(new SKSize(size, size))
            .GetAwaiter().GetResult();

        // 6. Make sure the active sprite is the drawing target (mirrors SkiaCanvas.OnViewportInitialized).
        sp.GetRequiredService<IDrawingService>().UpdateDrawingTarget();

        return new HeadlessHarness(sp, viewPort);
    }

    public void ActivateTool<TTool>() where TTool : ITool => _tools.ActivateTool<TTool>();

    public void SetColor(SKColor color) => _drawing.SetCurrentColor(color);

    /// <summary>Runs a command by name and waits for it to finish. Pumps the Avalonia dispatcher so
    /// async commands (e.g. those awaiting <c>Task.Delay</c>) can complete without a running message
    /// loop; rethrows the command's own exception, or times out after 2 s.</summary>
    public void Exec(string commandName)
        => PumpUntilComplete(Commands.ExecuteCommandAsync(commandName), TimeSpan.FromSeconds(2));

    private static void PumpUntilComplete(Task task, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            if (sw.Elapsed > timeout)
                throw new TimeoutException($"did not complete in {timeout.TotalSeconds:0}s (async work needs the UI loop?)");
            Thread.Sleep(2);
        }
        if (task.IsFaulted)
            ExceptionDispatchInfo.Capture(task.Exception!.InnerException ?? task.Exception!).Throw();
    }

    // --- Context setup so context-dependent commands can run in a sweep --------------------------
    /// <summary>Replaces the scene with a fresh single-artboard project and resyncs the drawing target,
    /// giving a deterministic clean state (used to reset before the command sweep).</summary>
    public void NewProject(int size = 64)
    {
        PumpUntilComplete(
            Services.GetRequiredService<IProjectService>().CreateNewProjectAsync(new SKSize(size, size)),
            TimeSpan.FromSeconds(5));
        _drawing.UpdateDrawingTarget();
    }

    /// <summary>Full-canvas pixel selection on the active layer — satisfies selection-dependent
    /// sprite commands (Crop, Fill selection, Apply/Transform selection, Rotate-with-selection).
    /// Resyncs the drawing target first: after tab/artboard switches it can be stale, and SelectAll
    /// against a stale target throws.</summary>
    public void EnsurePixelSelection()
    {
        _drawing.UpdateDrawingTarget();
        _drawing.SelectAll();
    }

    /// <summary>True while a pixel-selection marquee exists on the drawing layer.</summary>
    public bool HasPixelSelection => _drawing.DrawingLayer.HasSelection;

    /// <summary>The live drawing layer — lets a scenario read or tune the drawing-side settings a tool's
    /// settings panel writes to (fill opacity, selection tolerance, drawing mode).</summary>
    public Pix2d.Abstract.Drawing.IDrawingLayer DrawingLayer => _drawing.DrawingLayer;

    /// <summary>Marquee lifecycle phase (none / marquee ready / pixels lifted for transform).</summary>
    public Pix2d.Primitives.Drawing.SelectionPhase PixelSelectionPhase => _drawing.DrawingLayer.SelectionPhase;

    /// <summary>Bounding box of the current marquee in world coordinates (empty when there is none).</summary>
    public SKRect PixelSelectionBounds =>
        _drawing.DrawingLayer.HasSelection ? _drawing.DrawingLayer.GetSelectionLayer().GetBoundingBox() : SKRect.Empty;

    /// <summary>
    /// The marquee flattened to a canvas-space mask (1 = selected, indexed <c>x + y * canvasWidth</c>), or
    /// null when nothing is selected. Lets a scenario assert *which* pixels a combined selection covers —
    /// a bounding box can't tell a union from the rectangle that encloses it, nor spot a subtracted hole.
    /// </summary>
    public byte[]? PixelSelectionMask()
    {
        var layer = _drawing.DrawingLayer;
        if (!layer.HasSelection || layer.DrawingTarget is not { } target)
            return null;

        var size = target.GetSize();
        return Pix2d.Plugins.Drawing.Common.Drawing.SelectionMaskOps.Rasterize(
            (Pix2d.Plugins.Drawing.Nodes.SpriteSelectionNode)layer.GetSelectionLayer(),
            ((SKNode)target).Position,
            (int)size.Width,
            (int)size.Height);
    }

    /// <summary>True when canvas pixel (x, y) is inside the current selection.</summary>
    public bool IsPixelSelected(int x, int y)
    {
        var mask = PixelSelectionMask();
        if (mask == null || _drawing.DrawingLayer.DrawingTarget is not { } target)
            return false;

        var size = target.GetSize();
        if (x < 0 || y < 0 || x >= (int)size.Width || y >= (int)size.Height)
            return false;

        return mask[x + y * (int)size.Width] > 0;
    }

    /// <summary>Number of selected canvas pixels (0 when there is no selection).</summary>
    public int SelectedPixelCount()
    {
        var mask = PixelSelectionMask();
        if (mask == null)
            return 0;

        var count = 0;
        foreach (var v in mask)
            if (v > 0)
                count++;

        return count;
    }

    /// <summary>Drags a marquee with the active selection tool, from world (x0, y0) to world (x1, y1).</summary>
    public void DragWorld(float x0, float y0, float x1, float y1, KeyModifier modifiers = KeyModifier.None)
    {
        PressWorld(x0, y0, modifiers);
        MoveWorld((x0 + x1) / 2, (y0 + y1) / 2, pressed: true, modifiers);
        MoveWorld(x1, y1, pressed: true, modifiers);
        ReleaseWorld(x1, y1, modifiers);
    }

    /// <summary>Selects the first artboard as a scene-level node so object/General-context commands
    /// (arrange z-order) have a non-null <see cref="ISelectionService.Selection"/> to act on.</summary>
    public void EnsureNodeSelection()
    {
        var artboard = AppState.CurrentProject.SceneNode!.Nodes.OfType<Pix2dSprite>().FirstOrDefault();
        if (artboard != null)
            _selection.Select(artboard);
    }

    /// <summary>Every artboard on the current scene, in scene order.</summary>
    public Pix2dSprite[] Artboards =>
        AppState.CurrentProject.SceneNode!.Nodes.OfType<Pix2dSprite>().ToArray();

    /// <summary>The current object selection (General context).</summary>
    public SKNode[] SelectedNodes => AppState.CurrentProject.Selection?.Nodes ?? [];

    public void SelectNodes(params SKNode[] nodes) => _selection.Select(nodes);

    /// <summary>World bounds of the General-context object frame as it is actually drawn — the move thumb,
    /// which sizes itself from <c>NodesSelection.Frame</c> (a cached node), not from the live selection
    /// bounds. This is what goes stale when a selected artboard's canvas changes under it.</summary>
    public SKRect ObjectFrameBounds =>
        AppState.CurrentProject.FrameEditorNode is Pix2d.InteractiveNodes.FrameEditorNode frame
            ? frame.SelectionBounds
            : SKRect.Empty;

    /// <summary>The scriptable headless dialog surface — set <c>YesNoAnswer</c> to drive a confirmation.</summary>
    public HeadlessDialogService Dialogs => (HeadlessDialogService)Services.GetRequiredService<IDialogService>();

    public IArtboardObjectEditService CanvasEdit => Services.GetRequiredService<IArtboardObjectEditService>();

    /// <summary>Clicks an artboard's always-on name label (the <see cref="ArtboardLabelsLayer"/> hit target),
    /// which is how a user activates an artboard (single click) or edits it as an object (double click).</summary>
    public void ClickArtboardLabel(Pix2dSprite sprite, int clickCount = 1)
    {
        var rect = Pix2d.InteractiveNodes.ArtboardLabelsLayer.GetLabelRect(_viewPort, sprite);
        ClickWorld(rect.MidX, rect.MidY, clickCount: clickCount);
    }

    /// <summary>The live sprite editor for the active project — the same instance the timeline drives.</summary>
    public Pix2d.Plugins.Sprite.Editors.SpriteEditor SpriteEditor =>
        (Pix2d.Plugins.Sprite.Editors.SpriteEditor)AppState.CurrentProject.CurrentNodeEditor!;

    /// <summary>Selects a frame the way the timeline does (state + editor stay in step).</summary>
    public void SetFrameIndex(int index)
    {
        AppState.SpriteEditorState.CurrentFrameIndex = index;
        SpriteEditor.SetFrameIndex(index);
    }

    /// <summary>Drag-reorders a frame, i.e. what a timeline tile drag commits.</summary>
    public void ReorderFrames(int oldIndex, int newIndex) => SpriteEditor.ReorderFrames(oldIndex, newIndex);

    // --- Structural counts, read straight off the model tree --------------------------------------
    public int UndoStackSize => Operations.UndoOperationsCount;
    public int LayerCount => ActiveSprite.Nodes.OfType<Pix2dSprite.Layer>().Count();
    public int FrameCount => ActiveSprite.GetFramesCount();
    public int ArtboardCount => AppState.CurrentProject.SceneNode!.Nodes.OfType<Pix2dSprite>().Count();

    /// <summary>Renders the active artboard to PNG bytes through the real Png exporter (CPU Skia — no
    /// GPU/window). Runs on a threadpool thread so an <c>await</c> can never rendezvous with the idle
    /// UI dispatcher.</summary>
    public byte[] ExportActivePng(double scale = 1)
    {
        var nodes = Services.GetRequiredService<IExportService>().GetNodesToExport(scale).ToArray();
        var exporter = new PngImageExporter(Services.GetRequiredService<IFileService>());
        return Task.Run(async () =>
        {
            await using var stream = await exporter.ExportToStreamAsync(nodes, scale);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }).GetAwaiter().GetResult();
    }

    /// <summary>Commits a single pencil dab at sprite-local pixel (x, y) as one undoable operation.
    /// The sprite sits at scene world origin, so sprite-local (x, y) is world (x + 0.5, y + 0.5); we
    /// convert that to a viewport point via the live ViewPort transform so it's correct regardless of
    /// the current pan/zoom (ShowAll re-frames the view on project load).</summary>
    public void DrawPixel(int x, int y)
    {
        var p = _viewPort.WorldToViewport(new SKPoint(x + 0.5f, y + 0.5f));
        _input.SetPointerPressed(p, KeyModifier.None, isTouch: false);
        _input.SetPointerReleased(p, KeyModifier.None, isTouch: false);
    }

    /// <summary>Pins the camera to an explicit zoom/pan. Adorner thumbs size themselves in *screen*
    /// pixels (PixelsToWorld), so at the tiny 64px harness viewport a ShowAll zoom (~0.4) inflates
    /// their world-space hit zones enough to blanket whole artboards — gesture scenarios pin 1:1
    /// so hit zones stay proportionate, like on a real-sized viewport.</summary>
    public void SetView(float zoom, float panX = 0, float panY = 0)
    {
        _viewPort.SetZoom(zoom);
        _viewPort.SetPan(panX, panY);
    }

    // --- Raw pointer input at world coordinates (for tools that are driven by pointer gestures) ----
    public void PressWorld(float x, float y, KeyModifier modifiers = KeyModifier.None, int clickCount = 1)
        => _input.SetPointerPressed(_viewPort.WorldToViewport(new SKPoint(x, y)), modifiers, isTouch: false, clickCount);

    public void MoveWorld(float x, float y, bool pressed, KeyModifier modifiers = KeyModifier.None)
        => _input.SetPointerMoved(_viewPort.WorldToViewport(new SKPoint(x, y)), pressed, modifiers, isTouch: false);

    public void ReleaseWorld(float x, float y, KeyModifier modifiers = KeyModifier.None)
        => _input.SetPointerReleased(_viewPort.WorldToViewport(new SKPoint(x, y)), modifiers, isTouch: false);

    public void ClickWorld(float x, float y, KeyModifier modifiers = KeyModifier.None, int clickCount = 1)
    {
        PressWorld(x, y, modifiers, clickCount);
        ReleaseWorld(x, y, modifiers);
    }

    /// <summary>
    /// Holds / releases Shift in SKInput's *keyboard* modifier state — the source interactive scene nodes
    /// read (<c>SKInput.GetModifiers()</c>: the aspect lock of the artboard resize frame, SnappingService,
    /// ...). The per-pointer <c>modifiers</c> argument of Press/Move/ReleaseWorld does not feed it: pointer
    /// events pass modifiers along in their event args but never update the keyboard state.
    /// </summary>
    public void HoldShift() => _input.SetKeyPressed(VirtualKeys.Shift, KeyModifier.Shift);

    /// <inheritdoc cref="HoldShift"/>
    public void ReleaseShift() => _input.SetKeyReleased(VirtualKeys.Shift, KeyModifier.None);

    /// <summary>The active sprite (the artboard currently being edited).</summary>
    public Pix2dSprite ActiveSprite => (Pix2dSprite)AppState.CurrentProject.CurrentEditedNode!;

    /// <summary>Reads the committed color at sprite-local pixel (x, y) from the active layer/frame.</summary>
    public SKColor GetPixel(int x, int y) => ActiveSprite.PickColorByPoint(x, y);

    /// <summary>Diagnostic: every non-transparent pixel in the active frame, as (x, y, color).</summary>
    public IEnumerable<(int X, int Y, SKColor Color)> NonEmptyPixels()
    {
        var w = (int)ActiveSprite.Size.Width;
        var h = (int)ActiveSprite.Size.Height;
        var data = ActiveSprite.GetData(); // RGBA8888, row-major
        if (data.Length < w * h * 4) yield break;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = (y * w + x) * 4;
            if (data[i + 3] != 0)
                yield return (x, y, new SKColor(data[i], data[i + 1], data[i + 2], data[i + 3]));
        }
    }
}
