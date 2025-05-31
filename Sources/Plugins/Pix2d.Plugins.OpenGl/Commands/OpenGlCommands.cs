using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Pix2d.Plugins.OpenGl.Commands;

internal class OpenGlCommands : CommandsListBase
{
    protected override string BaseName => "Global.OpenGl";

    public Pix2dCommand ShowOpenGlDialog
        => GetCommand(() => ServiceProvider.GetRequiredService<IDialogService>().TogglePanelView(OpenGlPlugin.GetOpenGlPanel()),
            "Show OpenGL", default, EditContextType.All, commandName: "Toggle OpenGl Window");
}