using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;

namespace Pix2d.Plugins.PngCompress.Commands;

internal class PngCompressCommands : CommandsListBase
{
    protected override string BaseName => "Global.PngCompress";

    public Pix2dCommand ShowPngCompressDialog
        => GetCommand(() => ServiceProvider.GetRequiredService<IDialogService>().TogglePanelView(PngCompressPlugin.GetPngCompressPanel()), 
            "Capture image from camera", null, EditContextType.All, commandName: "Toggle png compress");
}