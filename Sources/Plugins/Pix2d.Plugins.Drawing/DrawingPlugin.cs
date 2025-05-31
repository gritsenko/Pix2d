using Pix2d.Abstract.Tools;
using Pix2d.Infrastructure;
using Pix2d.Plugins.Drawing.Commands;
using Pix2d.Plugins.Drawing.Services;
using Pix2d.Plugins.Drawing.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.Plugins.Drawing.Tools.Shapes;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.Drawing;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DrawingPlugin))]
[ServiceProviderPlugin<IDrawingService, DrawingService>]
public class DrawingPlugin(ICommandService commandService, IToolService toolService, IDrawingService drawingService)
    : IPix2dPlugin
{
    private readonly PixelSelectionCommands _pixelSelection = new();
    private readonly ToolCommands _toolCommands = new();
    private readonly DrawingBrushCommands _drawingBrushCommands = new();


    public void Initialize()
    {
        commandService.RegisterCommandList(_drawingBrushCommands);
        commandService.RegisterCommandList(_pixelSelection);
        commandService.RegisterCommandList(_toolCommands);

        drawingService.InitBrushSettings();

        RegisterTool<BrushTool>();

        RegisterTool<PixelLineTool>();
        RegisterTool<PixelRectangleTool>();
        RegisterTool<PixelOvalTool>();
        RegisterTool<PixelTriangleTool>();

        RegisterTool<EraserTool>();
        RegisterTool<FillTool>();

        RegisterTool<PixelSelectRectTool>();
        RegisterTool<PixelSelectLassoTool>();
        RegisterTool<PixelSelectColorTool>();

        RegisterTool<EyedropperTool>();

    }

    private void RegisterTool<T>() where T : ITool
    {
        toolService.RegisterTool<T>(EditContextType.Sprite);
    }
}