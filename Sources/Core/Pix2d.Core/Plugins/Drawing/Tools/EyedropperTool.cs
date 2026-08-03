using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    DisplayName = "Eyedropper tool",
    HotKey = "I")]
public class EyedropperTool : BaseTool, IDrawingTool
{
    private readonly AppState _appState;
    private readonly IToolService _toolService;

    public IDrawingService DrawingService { get; }

    public EyedropperTool(IDrawingService drawingService, IToolService toolService, AppState appState)
    {
        DrawingService = drawingService;
        _toolService = toolService;
        _appState = appState;
    }

    public override async Task Activate()
    {
        DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
        await base.Activate();
    }

    protected override void OnPointerReleased(object? sender, PointerActionEventArgs e)
    {
        e.Handled = true;
        DrawingService.PickColorByPoint(e.Pointer.WorldPosition);

        // #215: on touch, going back to the brush is a separate two-tap detour, so offer to do it here.
        // Opt-in — with the option off the eyedropper keeps its classic "stays until you switch" behaviour.
        if (_appState.IsReturnToPreviousToolAfterColorPickEnabled)
            _toolService.ActivatePreviousTool();
    }
}
