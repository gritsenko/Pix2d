using System;
using Microsoft.Extensions.DependencyInjection;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Messages;
using Pix2d.Primitives;
using Pix2d.State;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class ViewCommands : CommandsListBase
{
    public ViewCommands()
    {
    }
    protected override string BaseName => "View";

    public Pix2dCommand ZoomIn => GetCommand(() => ServiceProvider.GetRequiredService<IViewPortService>().ViewPort.ZoomIn(), "Zoom In", new CommandShortcut(VirtualKeys.OEMPlus), EditContextType.All);

    public Pix2dCommand ZoomOut => GetCommand(() => ServiceProvider.GetRequiredService<IViewPortService>().ViewPort.ZoomOut(), "Zoom Out", new CommandShortcut(VirtualKeys.OEMMinus), EditContextType.All);

    public Pix2dCommand ZoomAll => GetCommand(() =>
    {
        var vp = ServiceProvider.GetRequiredService<IViewPortService>().ViewPort;
        if (Math.Abs(vp.Zoom - 1) < 0.01)
            ServiceProvider.GetRequiredService<IViewPortService>().ShowAll();
        else
            vp.SetZoom(1);
    }, "Zoom All", new CommandShortcut(VirtualKeys.N0), EditContextType.All);

    public Pix2dCommand ToggleTimeline => GetCommand(() => AppState.UiState.ShowTimeline = !AppState.UiState.ShowTimeline, "Show/Hide timeline", new CommandShortcut(VirtualKeys.T, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand TogglePreviewPanelCommand => GetCommand(() => AppState.UiState.ShowPreviewPanel = !AppState.UiState.ShowPreviewPanel, "Toggle preview panel", new CommandShortcut(VirtualKeys.P, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand ToggleShowLayersCommand => GetCommand(() => AppState.UiState.ShowLayers = !AppState.UiState.ShowLayers, "Toggle Layers list", new CommandShortcut(VirtualKeys.L, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand HideExportDialogCommand => GetCommand(() => AppState.UiState.ShowExportDialog = false);
    public Pix2dCommand ShowExportDialogCommand => GetCommand(() => AppState.UiState.ShowExportDialog = true);

    public Pix2dCommand ToggleExtraToolsCommand =>
        GetCommand(() => AppState.UiState.ShowExtraTools = !AppState.UiState.ShowExtraTools);

    public Pix2dCommand ToggleTimelineCommand =>
        GetCommand(() => AppState.UiState.ShowTimeline = !AppState.UiState.ShowTimeline);

    public Pix2dCommand ShowMainMenuCommand => GetCommand(() => AppState.UiState.ShowMenu = true);
    public Pix2dCommand HideMainMenuCommand => GetCommand(() => AppState.UiState.ShowMenu = false);

    public Pix2dCommand ToggleMainMenuCommand =>
        GetCommand(() => AppState.UiState.ShowMenu = !AppState.UiState.ShowMenu);

    public Pix2dCommand ToggleCanvasSizePanelCommand => GetCommand(
        () => AppState.UiState.ShowCanvasResizePanel = !AppState.UiState.ShowCanvasResizePanel,
        behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand ToggleBrushSettingsCommand => GetCommand(() =>
    {
        var uiState = AppState.UiState;
        var isOpen = uiState.ShowBrushSettings;
        Messenger.Default.Send(new CloseUnpinnedPopups());
        uiState.ShowBrushSettings = !isOpen;
    }, "Brush settings", null, EditContextType.All);


    public Pix2dCommand ShowLayerOptionsCommand => GetCommand(() => AppState.UiState.ShowLayerProperties = true);
    public Pix2dCommand HideLayerOptionsCommand => GetCommand(() => AppState.UiState.ShowLayerProperties = false);

    public Pix2dCommand ToggleLayerOptionsCommand => GetCommand(() =>
        AppState.UiState.ShowLayerProperties = !AppState.UiState.ShowLayerProperties);

    public Pix2dCommand ShowClipboardBarCommand => GetCommand(() => AppState.UiState.ShowClipboardBar = true);
    public Pix2dCommand HideClipboardBarCommand => GetCommand(() => AppState.UiState.ShowClipboardBar = false);

    public Pix2dCommand ToggleClipboardBarCommand =>
        GetCommand(() => AppState.UiState.ShowClipboardBar = !AppState.UiState.ShowClipboardBar);

    public Pix2dCommand ToggleColorEditorCommand => GetCommand(() =>
    {
        var uiState = AppState.UiState;
        var isOpen = uiState.ShowColorEditor;
        Messenger.Default.Send(new CloseUnpinnedPopups());
        uiState.ShowColorEditor = !isOpen;
    }, "Select color", null, EditContextType.All);

    public Pix2dCommand ShowLicensePurchaseCommand => GetCommand(() =>
    {
        ServiceProvider.GetRequiredService<IMessenger>().Send(new ShowMenuItemMessage
            { ItemToShow = ShowMenuItemMessage.MenuItem.Licence });
    });

    public SnappingCommands Snapping { get; } = new();

    public Pix2dCommand ToggleFullScreenModeCommand =>
        GetCommand(() =>
        {
            ServiceProvider.GetRequiredService<IPlatformStuffService>().ToggleFullscreenMode();

        }, "Full screen", new CommandShortcut(VirtualKeys.F11), EditContextType.All);
}