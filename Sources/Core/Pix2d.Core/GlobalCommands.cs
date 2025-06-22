using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Platform;
using Pix2d.CommonNodes;
using Pix2d.Primitives;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;

namespace Pix2d;

public class GlobalCommands : CommandsListBase
{
    protected override string BaseName => "Global";

    public Pix2dCommand ExportLocalizedStrings
        => GetCommand(() => LocalizationService.ExportStrings(ServiceProvider.GetRequiredService<IFileService>()), "Export localized strings", null, EditContextType.All);

    public Pix2dCommand SwitchToFullMode
        => GetCommand(() =>
        {
            ServiceProvider.GetRequiredService<IEditService>().ApplyCurrentEdit();
            AppState.CurrentProject.CurrentContextType = EditContextType.General;
        }, "SwitchToFullMode", new CommandShortcut(VirtualKeys.F12, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand SwitchTo3dMode
        => GetCommand(() =>
        {
            AppState.CurrentProject.CurrentContextType = EditContextType.General3d;
            ServiceProvider.GetRequiredService<IEditService>().ApplyCurrentEdit();
        }, "SwitchTo3dMode", new CommandShortcut(VirtualKeys.F3, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand ToggleDebugMode
        => GetCommand(() => SkiaNodes.SKApp.DebugMode = !SkiaNodes.SKApp.DebugMode, "DebugMode", new CommandShortcut(VirtualKeys.F12, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand ShowLogsFolder
        => GetCommand(() => ServiceProvider.GetRequiredService<IPlatformStuffService>().OpenAppDataFolder(), "Show logs", null, EditContextType.All);

    public Pix2dCommand SwitchToSpriteMode
        => GetCommand(() =>
        {
            var selectedNodes = ServiceProvider.GetRequiredService<ISelectionService>()
                ?.Selection?.Nodes ?? AppState.CurrentProject.SceneNode.GetVisibleDescendants()
                .OfType<Pix2dSprite>().ToArray();

            if (selectedNodes.FirstOrDefault() is Pix2dSprite sprite)
            {
                ServiceProvider!.GetRequiredService<IEditService>().RequestEdit(selectedNodes);
            }
        }, "SwitchToSpriteMode", new CommandShortcut(VirtualKeys.F12, KeyModifier.Ctrl), EditContextType.General);
}