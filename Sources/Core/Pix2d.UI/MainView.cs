using System.Diagnostics;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Pix2d.Abstract.Import;
using Pix2d.Command;
using Pix2d.Common.FileSystem;
using Pix2d.Messages;
using Pix2d.UI.Animation;
using Pix2d.UI.BrushSettings;
using Pix2d.UI.Export;
using Pix2d.UI.Layers;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using Pix2d.UI.ToolBar;

namespace Pix2d.UI;

public class MainView : LocalizedComponentBase
{

    protected override StyleGroup BuildStyles() =>
    [
        new Style<TimeLineView>(s => s.Name("timeLine"))
            .RenderTransform(TransformOperations.Parse("translateY(30px)"))
            .IsVisible(false),
        new Style<TimeLineView>(s => s.Name("timeLine").Class("shown"))
            .RenderTransform(TransformOperations.Parse("translateY(0)"))
            .IsVisible(true),

        new Style<ToolBarView>()
            .Margin(StaticResources.Measures.PanelMargin,StaticResources.Measures.PanelMargin,StaticResources.Measures.PanelMargin,48)
            .Col(0)
            .Row(2)
            .RowSpan(2),

        new Style<LayersView>()
            .Margin(0,StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin)
            .VerticalAlignment(VerticalAlignment.Center)
            .RowSpan(1),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<LayersView>()
                .Margin(0,StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin + 56 + 12)
                .VerticalAlignment(VerticalAlignment.Bottom)
                .RowSpan(2),

            new Style<ToolBarView>()
                .Margin(StaticResources.Measures.PanelMargin)
                .Col(0)
                .Row(3)
                .RowSpan(1)
                .ColSpan(2),

            new Style<InfoPanelView>()
                .IsVisible(false),

            new Style<ZoomPanelView>()
                .IsVisible(false)
        }
    ];

    protected override object Build() =>
        new Grid()
            .Ref(out _rootGrid)
            .BindClass(() => Bounds.Width > 500, nameof(VisualStates.Wide))
            .BindClass(() => Bounds.Width <= 500, nameof(VisualStates.Narrow))
            .Cols("Auto, *, Auto")
            .Rows("Auto, Auto, *, Auto, Auto")
            .Children([
                new Border().Col(0).Row(0)
                    .ColSpan(3).RowSpan(5)
                    .With(self =>
                    {
                        self.AddHandler(PointerPressedEvent, (_, e) =>
                        {
                            if (e.Source is StyledElement element) Messenger.Send(new WindowClickedMessage(element));
                        }, RoutingStrategies.Tunnel);
                    })
                    .Name("Pix2dCanvasContainer"),

                new Border().Col(0).Row(0)
                    .IsVisible(() => AppState.CurrentProject.CurrentContextType == EditContextType.General3d)
                    .ColSpan(3).RowSpan(5)
                    .Child(new OpenGlView()),

#if DEBUG
                new AppMenuView().ColSpan(3),
#endif
                new TopBarView().Row(1).ColSpan(3)
                    .Margin(0, 0, 0, 1),

                new ToolBarView()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Center),

                new AdditionalTopBarView().Col(2).Row(3)
                    .Margin(0, 0, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin),

                new RatePromptView().Col(1).Row(2)
                    .IsVisible(() => AppState.UiState.ShowRatePrompt),

                new InfoPanelView().Col(0).Row(3).ColSpan(2)
                    .Margin(StaticResources.Measures.PanelMargin)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Bottom),

                new ZoomPanelView().Col(0).ColSpan(3).Row(3)
                    .Margin(StaticResources.Measures.PanelMargin)
                    .HorizontalAlignment(HorizontalAlignment.Center),


                new Grid().Col(0).ColSpan(3).Row(2).Rows("auto,auto")
                    .Margin(StaticResources.Measures.PanelMargin)
                    .Children(
                        new ActionsBarView()
                            .IsVisible(UiState.ShowExtraTools, bindingSource: UiState)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top),

                        //new ClipboardActionsView().Row(1)
                        //    .IsVisible(UiState.ShowClipboardBar, bindingSource: UiState)
                        //    .HorizontalAlignment(HorizontalAlignment.Center)
                        //    .VerticalAlignment(VerticalAlignment.Top),

                        new TopToolUiContainer().Row(1)
                            //.IsVisible(() => UiState.TopToolUi != null)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Top)
                    ),

                new TimeLineView()
                    {
                        Transitions = new Transitions
                        {
                            new TransformOperationsTransition()
                            {
                                Property = TimeLineView.RenderTransformProperty,
                                Duration = TimeSpan.FromSeconds(0.3),
                                Easing = new BackEaseOut()
                            }
                        }
                    }
                    .Col(0).Row(4).Name("timeLine")
                    .ColSpan(3)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .BindClass(UiState.ShowTimeline, "shown", bindingSource: UiState),

                new LayersView().Col(2).Row(2)
                    .IsVisible(UiState.ShowLayers, bindingSource: UiState)
                    .HorizontalAlignment(HorizontalAlignment.Right),

                //new Border().Col(1).Row(2).Background(Colors.BurlyWood.ToBrush()),
                new Canvas().Col(1).Row(2).Name("PopupContainer")
                    .Ref(out _panelsContainer)
                    .Children(new Control[]
                    {

                        new PopupView().Name("ColorPicker")
                            .Header(L("Color"))
                            .Canvas_Top(10)
                            .Canvas_Left(10)
                            .IsOpen(UiState.ShowColorEditor, BindingMode.TwoWay, bindingSource: UiState)
                            .CloseButtonCommand(ViewCommands.ToggleColorEditorCommand)
                            .ShowPinButton(true)
                            .Content(new ColorPickerView()),

                        new PopupView().Name("BrushSettings")
                            .Header(L("Brush"))
                            .IsOpen(UiState.ShowBrushSettings, BindingMode.TwoWay, bindingSource: UiState)
                            .CloseButtonCommand(ViewCommands.ToggleBrushSettingsCommand)
                            .Width(258)
                            .ShowPinButton(true)
                            .Content(new BrushSettingsView()),

                        new PopupView().Name("ArtworkPreview")
                            .Header(L("Preview"))
                            .IsOpen(UiState.ShowPreviewPanel, bindingSource: UiState)
                            .CloseButtonCommand(ViewCommands.TogglePreviewPanelCommand)
                            .Canvas_Top(40)
                            .Canvas_Right(100)
                            .Content(new ArtworkPreviewView()),

                        new PopupView()
                            .Header(L("Image/Canvas size"))
                            .IsOpen(UiState.ShowCanvasResizePanel, bindingSource: UiState)
                            .CloseButtonCommand(ViewCommands.ToggleCanvasSizePanelCommand)
                            .Width(220)
                            .Canvas_Top(100)
                            .Canvas_Right(100)
                            .Content(new ResizeCanvasView().Ref(out var resizeCanvasView))
                            .OnShow(() => resizeCanvasView.UpdateData()),

                        new PopupView()
                            .Header(L("Layer options"))
                            .IsOpen(UiState.ShowLayerProperties, bindingSource: UiState)
                            .CloseButtonCommand(ViewCommands.HideLayerOptionsCommand)
                            .Width(300)
                            .Canvas_Top(40)
                            .Canvas_Right(100)
                            .Content(new LayerOptionsView())

                    }),

                new ToolGroupContainerView()
                    .Col(1).Row(2)
                    .IsVisible(UiState.ShowToolGroup, bindingSource: UiState)
                    .Margin(left: 8, top: 120)
                    .MinWidth(40)
                    .MinHeight(40)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top),

                new ExportView().ColSpan(3).RowSpan(5).IsVisible(UiState.ShowExportDialog, bindingSource: UiState),

                new Border().Name("MainMenuContainer") //MAIN MENU
                    .Col(0).ColSpan(3)
                    .Row(0).RowSpan(5)
                    .IsVisible(UiState.ShowMenu, bindingSource: UiState)
                    .Child(
                        new MainMenuView()),

                new Border().Name("LoadingOverlay")
                    .Col(0).ColSpan(3)
                    .Row(0).RowSpan(4)
                    .IsVisible(AppState.IsBusy, bindingSource: AppState)
                    .Background(StaticResources.Brushes.ModalOverlayBrush)
                    .Child(
                        new TextBlock()
                            .Text(L("Working..."))
                            .VerticalAlignment(VerticalAlignment.Center)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                    ),

                new DialogContainer()
                    .Col(0).ColSpan(3)
                    .Row(0).RowSpan(5)
            ]);

    private Canvas _panelsContainer = null!;
    private Grid _rootGrid = null!;
    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private IProjectService ProjectService { get; set; } = null!;
    [Inject] private IImportService ImportService { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;
    private UiState UiState => AppState.UiState;
    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;

    protected override void OnBeforeReload()
    {
        Debug.WriteLine(" Reloading main view...");
        base.OnBeforeReload();
    }

    protected override void OnAfterInitialized()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        DialogService.SetPanelsContainer(_panelsContainer);
        AppState.UiState.WatchFor(x => x.ShowRatePrompt, StateHasChanged);
        AppState.CurrentProject.WatchFor(x => x.CurrentContextType, StateHasChanged);

        StateHasChanged();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        //force recalculation on window width to check if it's Narrow state now
        StateHasChanged();
        AppState.UiState.VisualState = _rootGrid.Classes.Contains(nameof(VisualStates.Narrow)) ? nameof(VisualStates.Narrow) : nameof(VisualStates.Wide);
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
        //if we dropped files to some panel that accepts drops
        if (e.Handled)
            return;

        var data = e.Data.Get("Files");

        if (data is not IEnumerable<IStorageItem> droppedFiles)
            return;

        foreach (var storageFile in droppedFiles.OfType<IStorageFile>())
        {
            var path = System.Net.WebUtility.UrlDecode(storageFile.Path.AbsolutePath);

            var fileSource = new NetFileSource(path);

            if (path.EndsWith(".pxm") || path.EndsWith(".pix2d"))
            {
                await ProjectService.OpenFilesAsync([fileSource]);
                return;
            }

            if (AppState.CurrentProject.CurrentNodeEditor is not IImportTarget importTarget)
                throw new ArgumentException("Import target is required");

            await ImportService.ImportAsync([fileSource], importTarget);
        }
    }
}