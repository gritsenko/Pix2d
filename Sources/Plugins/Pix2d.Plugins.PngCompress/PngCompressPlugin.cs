using Pix2d.Abstract;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Abstract.Services;
using Pix2d.Plugins.PngCompress.Commands;
using Pix2d.Plugins.PngCompress.UI;

namespace Pix2d.Plugins.PngCompress;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(PngCompressPlugin))]
public class PngCompressPlugin(ICommandService commandService) : IPix2dPlugin
{
    private static PngCompressView? _panelInstance; 
    public void Initialize()
    {
        commandService.RegisterCommandList(new PngCompressCommands());
 
        //toolService.RegisterTool<YourTool>(EditContextType.Sprite);
    }

    public static PngCompressView GetPngCompressPanel()
    {
        _panelInstance ??= new PngCompressView();
        return _panelInstance;
    }
}