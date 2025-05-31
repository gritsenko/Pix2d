using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Simple.Commands;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.Simple;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(SimplePlugin))]
public class SimplePlugin(IDrawingService drawingService, IToolService toolService, ICommandService commandService)
    : IPix2dPlugin
{
    public void Initialize()
    {
        commandService.RegisterCommandList(new SimpleCommands());

        //toolService.RegisterTool<YourTool>(EditContextType.Sprite);
    }
}