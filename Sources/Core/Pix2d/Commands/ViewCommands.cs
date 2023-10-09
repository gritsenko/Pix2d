using System;
using Pix2d.Abstract.Commands;
using Pix2d.Messages;
using Pix2d.Primitives;
using Pix2d.Views;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class ViewCommands : CommandsListBase
{
    protected override string BaseName => "View";

    public Pix2dCommand ZoomIn => GetCommand("Zoom In",
        new CommandShortcut(VirtualKeys.OEMPlus),
        EditContextType.All,
        () => CoreServices.ViewPortService.ViewPort.ZoomIn());

    public Pix2dCommand ZoomOut => GetCommand("Zoom Out",
        new CommandShortcut(VirtualKeys.OEMMinus),
        EditContextType.All,
        () => CoreServices.ViewPortService.ViewPort.ZoomOut());

    public Pix2dCommand ZoomAll => GetCommand("Zoom All",
        new CommandShortcut(VirtualKeys.N0),
        EditContextType.All,
        () =>
        {
            var vp = CoreServices.ViewPortService.ViewPort;
            if (Math.Abs(vp.Zoom - 1) < 0.01)
                CoreServices.ViewPortService.ShowAll();
            else
                vp.SetZoom(1);
        });

    public Pix2dCommand ToggleTimeline => GetCommand("Show/Hide timeline",
        new CommandShortcut(VirtualKeys.T, KeyModifier.Ctrl),
        EditContextType.All,
        () => AppState.UiState.ShowTimeline = !AppState.UiState.ShowTimeline);

    public Pix2dCommand TogglePreviewPanelCommand => GetCommand("Toggle preview panel",
        new CommandShortcut(VirtualKeys.P, KeyModifier.Ctrl),
        EditContextType.Sprite,
        () => AppState.UiState.ShowPreviewPanel = !AppState.UiState.ShowPreviewPanel);

    public Pix2dCommand ToggleShowLayersCommand => GetCommand("Toggle Layers list",
        new CommandShortcut(VirtualKeys.L, KeyModifier.Ctrl),
        EditContextType.Sprite,
        () => AppState.UiState.ShowLayers = !AppState.UiState.ShowLayers);

    public Pix2dCommand HideExportDialogCommand => GetCommand(() => AppState.UiState.ShowExportDialog = false);
    public Pix2dCommand ShowExportDialogCommand => GetCommand(() => AppState.UiState.ShowExportDialog = true);
    public Pix2dCommand ToggleExtraToolsCommand => GetCommand(() => AppState.UiState.ShowExtraTools = !AppState.UiState.ShowExtraTools);
    public Pix2dCommand ToggleTimelineCommand => GetCommand(() => AppState.UiState.ShowTimeline = !AppState.UiState.ShowTimeline);

    public Pix2dCommand ShowMainMenuCommand => GetCommand(() => AppState.UiState.ShowMenu = true);
    public Pix2dCommand HideMainMenuCommand => GetCommand(() => AppState.UiState.ShowMenu = false);
    public Pix2dCommand ToggleMainMenuCommand => GetCommand(() => AppState.UiState.ShowMenu = !AppState.UiState.ShowMenu);

    public Pix2dCommand ToggleCanvasSizePanelCommand => GetCommand(() => AppState.UiState.ShowCanvasResizePanel = !AppState.UiState.ShowCanvasResizePanel, behaviour: DisableOnAnimation.Instance);
    public Pix2dCommand ToggleBrushSettingsCommand => GetCommand("Brush settings", 
        null,
        EditContextType.All,
        () =>
        {
            var uiState = AppState.UiState;
            var isOpen = uiState.ShowBrushSettings;
            Messenger.Default.Send(new CloseUnpinnedPopups());
            uiState.ShowBrushSettings = !isOpen;
        });


    public Pix2dCommand ShowLayerOptionsCommand => GetCommand(() => AppState.UiState.ShowLayerProperties = true);
    public Pix2dCommand HideLayerOptionsCommand => GetCommand(() => AppState.UiState.ShowLayerProperties = false);
    public Pix2dCommand ToggleLayerOptionsCommand => GetCommand(() => AppState.UiState.ShowLayerProperties = !AppState.UiState.ShowLayerProperties);

    public Pix2dCommand ShowClipboardBarCommand => GetCommand(() => AppState.UiState.ShowClipboardBar = true);
    public Pix2dCommand HideClipboardBarCommand => GetCommand(() => AppState.UiState.ShowClipboardBar = false);
    public Pix2dCommand ToggleClipboardBarCommand => GetCommand(() => AppState.UiState.ShowClipboardBar = !AppState.UiState.ShowClipboardBar);

    public Pix2dCommand ToggleColorEditorCommand => GetCommand("Select color", null, EditContextType.All, () =>
    {
        var uiState = AppState.UiState;
        var isOpen = uiState.ShowColorEditor;
        Messenger.Default.Send(new CloseUnpinnedPopups());
        uiState.ShowColorEditor = !isOpen;
    });

    public SnappingCommands Snapping { get; } = new();
}