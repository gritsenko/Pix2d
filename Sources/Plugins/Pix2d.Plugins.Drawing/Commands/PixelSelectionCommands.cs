using Microsoft.Extensions.DependencyInjection;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.Services;

namespace Pix2d.Plugins.Drawing.Commands;

public class PixelSelectionCommands : CommandsListBase
{
    protected override string BaseName => "Edit.Selection";

    public Pix2dCommand SelectAll => GetCommand(() =>
    {
        ServiceProvider?.GetRequiredService<ToolService>().ActivateTool<PixelSelectToolBase>();
        ServiceProvider?.GetRequiredService<IDrawingService>().SelectAll();
        ServiceProvider?.GetRequiredService<IViewPortRefreshService>().Refresh();
    }, "Select all", new CommandShortcut(VirtualKeys.A, KeyModifier.Ctrl), EditContextType.Sprite);

}