using Pix2d.Abstract;
using Pix2d.Command;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives;
using Pix2d.Primitives.Drawing;
using SkiaNodes.Interactive;

namespace Pix2d.Plugins.Drawing.Commands
{
    public class ToolCommands : CommandsListBase
    {
        protected override string BaseName => "Tools";

        public Pix2dCommand ActivateBrushTool => GetCommand(
            "Brush tool",
            new CommandShortcut(VirtualKeys.B),
            EditContextType.Sprite,
            () => CoreServices.ToolService.ActivateTool(nameof(BrushTool)));

        public Pix2dCommand ActivateEraserTool => GetCommand(
            "Eraser tool",
            new CommandShortcut(VirtualKeys.E),
            EditContextType.Sprite,
            () => CoreServices.ToolService.ActivateTool(nameof(EraserTool)));

        public Pix2dCommand ActivateFillTool => GetCommand(
            "Fill tool",
            new CommandShortcut(VirtualKeys.G),
            EditContextType.Sprite,
            () => CoreServices.ToolService.ActivateTool(nameof(FillTool)));

        public Pix2dCommand ActivatePixelSelectTool => GetCommand(
            "ActivatePixelSelectTool",
            new CommandShortcut(VirtualKeys.M),
            EditContextType.Sprite,
            () => CoreServices.ToolService.ActivateTool(nameof(PixelSelectTool)));

        public Pix2dCommand ActivateEyedropperTool => GetCommand(
            "ActivateEyedropperTool",
            new CommandShortcut(VirtualKeys.I),
            EditContextType.Sprite,
            () => CoreServices.ToolService.ActivateTool(nameof(EyedropperTool)));

        public Pix2dCommand ActivateLineTool => GetCommand(
            "Draw line",
            new CommandShortcut(VirtualKeys.L),
            EditContextType.Sprite,
            () => ActivateShapeSubTool(ShapeType.Line));
        
        public Pix2dCommand ActivateOvalTool => GetCommand(
            "Draw oval",
            new CommandShortcut(VirtualKeys.O),
            EditContextType.Sprite,
            () => ActivateShapeSubTool(ShapeType.Oval));
        
        public Pix2dCommand ActivateRectangleTool => GetCommand(
            "Draw rectangle",
            new CommandShortcut(VirtualKeys.R),
            EditContextType.Sprite,
            () => ActivateShapeSubTool(ShapeType.Rectangle));
        
        public Pix2dCommand ActivateTriangleTool => GetCommand(
            "Draw triangle",
            new CommandShortcut(VirtualKeys.T),
            EditContextType.Sprite,
            () => ActivateShapeSubTool(ShapeType.Triangle));

        public static void ActivateShapeSubTool(ShapeType? shapeType)
        {
            CoreServices.ToolService.ActivateTool(nameof(BrushTool));
            if (shapeType.HasValue && AppState.CurrentProject.CurrentTool is BrushTool brushTool)
                brushTool.ShapeType = shapeType.Value;
        }


    }

}