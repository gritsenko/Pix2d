using Pix2d.Abstract.Tools;
using Pix2d.Abstract;
using Pix2d.Drawing.Tools;

namespace Pix2d.Plugins.PixelText;

public class PixelTextPlugin : IPix2dPlugin
{
    public IToolService ToolService { get; }

    public PixelTextPlugin(IToolService toolService)
    {
        ToolService = toolService;
    }

    public void Initialize()
    {
        ToolService.RegisterTool<PixelTextTool>(EditContextType.Sprite);
    }

}