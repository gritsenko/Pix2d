using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.Primitives.Drawing;

namespace Pix2d.Plugins.Drawing.Commands;

public class ToolCommands : CommandsListBase
{
    protected override string BaseName => "Tools";

    public Pix2dCommand ActivateBrushTool =>
        GetCommand(() => ServiceProvider.GetRequiredService<IToolService>().ActivateTool<BrushTool>(), "Brush tool",
            new CommandShortcut(VirtualKeys.B), EditContextType.Sprite);

    public Pix2dCommand ActivateEraserTool =>
        GetCommand(() => ServiceProvider.GetRequiredService<IToolService>().ActivateTool<EraserTool>(), "Eraser tool",
            new CommandShortcut(VirtualKeys.E), EditContextType.Sprite);

    public Pix2dCommand ActivateFillTool =>
        GetCommand(() => ServiceProvider.GetRequiredService<IToolService>().ActivateTool<FillTool>(), "Fill tool",
            new CommandShortcut(VirtualKeys.G), EditContextType.Sprite);

    public Pix2dCommand ActivatePixelSelectTool => GetCommand(
        () => ServiceProvider.GetRequiredService<IToolService>().ActivateTool<PixelSelectToolBase>(), "Select tool",
        new CommandShortcut(VirtualKeys.M), EditContextType.Sprite);

    public Pix2dCommand ActivateEyedropperTool => GetCommand(
        () => ServiceProvider.GetRequiredService<IToolService>().ActivateTool<EyedropperTool>(), "Eyedroppper tool",
        new CommandShortcut(VirtualKeys.I), EditContextType.Sprite);

    public Pix2dCommand ActivateLineTool => GetCommand(() => ActivateShapeSubTool(ShapeType.Line), "Draw line",
        new CommandShortcut(VirtualKeys.L), EditContextType.Sprite);

    public Pix2dCommand ActivateOvalTool => GetCommand(() => ActivateShapeSubTool(ShapeType.Oval), "Draw oval",
        new CommandShortcut(VirtualKeys.O), EditContextType.Sprite);

    public Pix2dCommand ActivateRectangleTool => GetCommand(() => ActivateShapeSubTool(ShapeType.Rectangle),
        "Draw rectangle", new CommandShortcut(VirtualKeys.R), EditContextType.Sprite);

    public Pix2dCommand ActivateTriangleTool => GetCommand(() => ActivateShapeSubTool(ShapeType.Triangle),
        "Draw triangle", new CommandShortcut(VirtualKeys.T), EditContextType.Sprite);

    private void ActivateShapeSubTool(ShapeType? shapeType)
    {
        ServiceProvider.GetRequiredService<IToolService>().ActivateTool<BrushTool>();
        //if (shapeType.HasValue && AppState.ToolsState.CurrentTool.ToolInstance is BrushTool brushTool)
        //    brushTool.ShapeType = shapeType.Value;
    }
}