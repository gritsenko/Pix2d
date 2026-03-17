using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;

namespace Pix2d.Plugins.Drawing.Commands;

public class PixelSelectionCommands : CommandsListBase
{
    protected override string BaseName => "Edit.Selection";

    public Pix2dCommand SelectAll => GetCommand(() =>
    {
        ServiceProvider?.GetRequiredService<IToolService>().ActivateTool<PixelSelectRectTool>();
        ServiceProvider?.GetRequiredService<IDrawingService>().SelectAll();
        ServiceProvider?.GetRequiredService<IViewPortRefreshService>().Refresh();
    }, "Select all", new CommandShortcut(VirtualKeys.A, KeyModifier.Ctrl), EditContextType.Sprite);

}