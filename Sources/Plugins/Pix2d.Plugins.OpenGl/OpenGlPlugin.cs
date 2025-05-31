using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.OpenGl.Commands;
using Pix2d.Plugins.OpenGl.UI;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.OpenGl;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(OpenGlPlugin))]
public class OpenGlPlugin(IDrawingService drawingService, IToolService toolService, ICommandService commandService)
    : IPix2dPlugin
{
    private static OpenGlView _panelInstance;

    public void Initialize()
    {
        commandService.RegisterCommandList(new OpenGlCommands());
    }

    public static OpenGlView GetOpenGlPanel()
    {
        _panelInstance ??= new OpenGlView();
        return _panelInstance;
    }
}