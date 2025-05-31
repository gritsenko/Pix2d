using Microsoft.Extensions.DependencyInjection;

namespace Pix2d.Plugins.Drawing.Commands;

public class DrawingBrushCommands : CommandsListBase
{
    protected override string BaseName => "Drawing.Brush";

    public Pix2dCommand DecreaseBrushSize => GetCommand(() => ServiceProvider.GetRequiredService<IDrawingService>().ChangeBrushSize(-1),
        "Decrease brush size", new CommandShortcut(VirtualKeys.OEM4), EditContextType.Sprite);

    public Pix2dCommand IncreaseBrushSize => GetCommand(() => ServiceProvider.GetRequiredService<IDrawingService>().ChangeBrushSize(1),
        "Increase brush size", new CommandShortcut(VirtualKeys.OEM6), EditContextType.Sprite);
}