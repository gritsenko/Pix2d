using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
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
