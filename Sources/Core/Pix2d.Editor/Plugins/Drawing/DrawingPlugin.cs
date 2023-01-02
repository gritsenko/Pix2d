using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Tools;
using Pix2d.Infrastructure;
using Pix2d.Plugins.Drawing.Commands;
using Pix2d.Services;

namespace Pix2d.Plugins.Drawing
{
    [ServiceProviderPlugin(typeof(IDrawingService), typeof(DrawingService))]
    public class DrawingPlugin : IPix2dPlugin
    {
        public ICommandService CommandService { get; }
        public IToolService ToolService { get; }
        public PixelSelectionCommands PixelSelection { get; } = new PixelSelectionCommands();
        public ToolCommands ToolCommands { get; } = new ToolCommands();
        public DrawingBrushCommands DrawingBrushCommands { get; } = new DrawingBrushCommands();


        public DrawingPlugin(ICommandService commandService, IToolService toolService)
        {
            CommandService = commandService;
            ToolService = toolService;
        }

        public void Initialize()
        {
            CommandService.RegisterCommandList(DrawingBrushCommands);
            CommandService.RegisterCommandList(PixelSelection);
            CommandService.RegisterCommandList(ToolCommands);

            CoreServices.DrawingService.InitBrushSettings();

            RegisterTool<BrushTool>();
            RegisterTool<EraserTool>();
            RegisterTool<FillTool>();
            RegisterTool<PixelSelectTool>();
            RegisterTool<PixelTextTool>();
            RegisterTool<EyedropperTool>();

        }

        private void RegisterTool<T>() where T : ITool
        {
            ToolService.RegisterTool<T>(EditContextType.Sprite);
        }
    }
}
