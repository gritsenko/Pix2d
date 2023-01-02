using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Ai;

public class AiPlugin : IPix2dPlugin
{
    public IDrawingService DrawingService { get; }
    public IToolService ToolService { get; }

    public AiPlugin(IDrawingService drawingService, IToolService toolService)
    {
        DrawingService = drawingService;
        ToolService = toolService;
    }


    public void Initialize()
    {
        
        //ToolService.RegisterTool(EditContextType.Sprite)
    }
}