using System.Diagnostics;
using System.Linq;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Xaml.Interactions.Responsive;
using CommonServiceLocator;
using Pix2d.Common.FileSystem;
using Pix2d.Messages;
using Pix2d.UI.Animation;
using Pix2d.UI.BrushSettings;
using Pix2d.UI.Common.Extensions;
using Pix2d.UI.Export;
using Pix2d.UI.Layers;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.ToolBar;

namespace Pix2d.UI;

public class MainView : ComponentBase
{
    [Inject] private AppState? AppState { get; set; }
    private UiState? UiState => AppState?.UiState;

    public MainView()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var hasFiles = e.Data.GetDataFormats().Any(x => x == "Files");
        if (hasFiles)
            e.DragEffects = DragDropEffects.Copy;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var data = e.Data.Get("Files");

        if (data == null)
            return;

        var droppedFiles = data as IEnumerable<IStorageItem>;

        foreach (var storageFile in droppedFiles.OfType<IStorageFile>())
        {
            var path = System.Net.WebUtility.UrlDecode(storageFile.Path.AbsolutePath);

            var fileSource = new NetFileSource(path);

            if (path.EndsWith(".pxm") || path.EndsWith(".pix2d"))
            {
                var ps = ServiceLocator.Current.GetInstance<IProjectService>();
                await ps.OpenFilesAsync(new[] { fileSource });
                return;
            }

            var importSrv = ServiceLocator.Current.GetInstance<IImportService>();
            await importSrv.ImportToScene(new[] { fileSource });

        }
    }

    protected override object Build() =>
        new Grid()
            .Cols("Auto, *, Auto")
            .Rows("Auto, Auto, *, Auto, Auto")
            .AddBehavior(
                new AdaptiveBehavior()
                    .Setters(
                        new AdaptiveClassSetter() { MinWidth = 0, MaxWidth = 400, ClassName = "small" },
                        new AdaptiveClassSetter()
                            { MinWidth = 400, MaxWidth = double.PositiveInfinity, ClassName = "wide" }
                    )
            )
            .Children(new Control[]
            {

                new Border().Col(0).Row(0)
                    .ColSpan(2).RowSpan(5)
                    .With(self =>
                    {
                        self.AddHandler(PointerPressedEvent, (_, e) =>
                        {
                            if (e.Source is StyledElement element)
                            {
                                ServiceLocator.Current.GetInstance<IMessenger>()
                                    ?.Send(new WindowClickedMessage(element));
                            }
                        }, RoutingStrategies.Tunnel);
                    })
                    .Name("Pix2dCanvasContainer"),

#if DEBUG
                new AppMenuView().ColSpan(2),
#endif
                new TopBarView().Row(1).ColSpan(2)
                    .Margin(0, 0, 0, 1),

                new Border().Name("toolbar")
                    .BorderThickness(0, 0, 1, 0)
                    .Child(
                        new ScrollViewer()
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                            .Content(
                                LayoutableExtensions.HorizontalAlignment(new ToolBarView(), HorizontalAlignment.Left)
                            )
                    ),

                new AdditionalTopBarView().Col(1).Row(2),

                new RatePromptView().Col(1).Row(2)
                    .IsVisible(() => AppState?.UiState.ShowRatePrompt ?? false),

                new InfoPanelView().Col(0).Row(4).ColSpan(2),

                new Grid().Col(1).Row(2).Rows("auto,auto")
                    .Children(
                        new ActionsBarView()
                            .IsVisible(UiState.ShowExtraTools, bindingSource: UiState)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top),

                        new ClipboardActionsView().Row(1)
                            .IsVisible(UiState.ShowClipboardBar, bindingSource: UiState)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top),

                        new TopToolUiContainer().Row(1)
                            //.IsVisible(() => UiState.TopToolUi != null)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top)
                    ),

                //new TextBarView().Col(1).Row(2)
                //    .IsVisible(UiState.ShowTextBar, bindingSource: UiState)
                //    .HorizontalAlignment(HorizontalAlignment.Center)
                //    .VerticalAlignment(VerticalAlignment.Top),

                //new RatePromptView().Col(1).Row(2),

                new TimeLineView().Col(1).Row(3)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .IsVisible(UiState.ShowTimeline, bindingSource: UiState),

                new LayersView().Col(1).Row(2)
                    .IsVisible(UiState.ShowLayers, bindingSource: UiState)
                    .Margin(0, 33)
                    .HorizontalAlignment(HorizontalAlignment.Right),

                new Canvas().Col(1).Row(2)
                    .Children(new Control[]
                    {

                        new PopupView()
                            .Header("Color edit")
                            .Top(10)
                            .Left(10)
                            .IsOpen(UiState.ShowColorEditor, BindingMode.TwoWay, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.ToggleColorEditorCommand)
                            .ShowPinButton(true)
                            .Content(new ColorPickerView()),

                        new PopupView()
                            .Header("Brush settings")
                            .IsOpen(UiState.ShowBrushSettings, BindingMode.TwoWay, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.ToggleBrushSettingsCommand)
                            .Width(210)
                            .ShowPinButton(true)
                            .Content(new BrushSettingsView()),

                        new PopupView()
                            .Header("Preview")
                            .IsOpen(UiState.ShowPreviewPanel, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.TogglePreviewPanelCommand)
                            .Top(40)
                            .Right(100)
                            .Content(new ArtworkPreviewView()),

                        new PopupView()
                            .Header("Image/Canvas size")
                            .IsOpen(UiState.ShowCanvasResizePanel, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.ToggleCanvasSizePanelCommand)
                            .Width(220)
                            .Top(100)
                            .Right(100)
                            .Content(new ResizeCanvasView().Ref(out var _resizeCanvasView))
                            .OnShow(() => _resizeCanvasView.UpdateData()),

                        new PopupView()
                            .Header("Layer options")
                            .IsOpen(UiState.ShowLayerProperties, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.HideLayerOptionsCommand)
                            .Width(220)
                            .Top(40)
                            .Right(100)
                            .Content(new LayerOptionsView())

                    }),

                new ToolGroupContainerView()
                    .Col(1).Row(2)
                    .IsVisible(UiState.ShowToolGroup, bindingSource: UiState)
                    .Margin(8, 120, 0, 0)
                    .MinWidth(40)
                    .MinHeight(40)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top),

                new ToolSettingsContainerView()
                    .Col(1).Row(2)
                    .IsVisible(UiState.ShowToolProperties, bindingSource: UiState)
                    .Margin(8, 120, 0, 0)
                    .MinWidth(40)
                    .MinHeight(40)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top),


                new ExportView().ColSpan(2).RowSpan(5).IsVisible(UiState.ShowExportDialog, bindingSource: UiState),
                // new PopupView()
                //     .Col(0).ColSpan(2)
                //     .Row(0).RowSpan(4)
                //     .Header("Export artwork")
                //     .IsOpen(UiState.ShowExportDialog, bindingSource: UiState)
                //     .CloseButtonCommand(Commands.View.HideExportDialogCommand)
                //     .Content(new ExportView()),

                new Border() //MAIN MENU
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(5)
                    .IsVisible(UiState.ShowMenu, bindingSource: UiState)
                    .Child(
                        new MainMenuView(UiState)),

                new Border()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(4)
                    .IsVisible(AppState.IsBusy, bindingSource: AppState)
                    .Background(StaticResources.Brushes.ModalOverlayBrush)
                    .Child(
                        new TextBlock()
                            .Text("Working...")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                    ),

                new DialogContainer()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(5)
            });


    protected override void OnBeforeReload()
    {
        Debug.WriteLine(" Reloading main view...");
        base.OnBeforeReload();
    }

    protected override void OnAfterInitialized()
    {
        AppState.UiState.WatchFor(x => x.ShowRatePrompt, StateHasChanged);
    }
}