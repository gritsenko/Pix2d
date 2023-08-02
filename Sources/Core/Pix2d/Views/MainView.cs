using Avalonia.Xaml.Interactions.Responsive;
using Pix2d.Shared;
using Pix2d.Views.Animation;
using Pix2d.Views.BrushSettings;
using Pix2d.Views.Export;
using Pix2d.Views.Layers;
using Pix2d.Views.MainMenu;
using Pix2d.Views.Text;
using Pix2d.Views.ToolBar;
using Pix2d.Views.ToolBar.Tools;

namespace Pix2d.Views;

public class MainView : ComponentBase
{
    [Inject] private AppState? AppState { get; set; }
    private UiState? UiState => AppState?.UiState;

    protected override object Build() =>
        new Grid()
            .Cols("Auto, *, Auto")
            .Rows("Auto, Auto, *, 32")
            .AddBehavior(
                new AdaptiveBehavior()
                    .Setters(
                        new AdaptiveClassSetter() { MinWidth = 0, MaxWidth = 400, ClassName = "small" },
                        new AdaptiveClassSetter() { MinWidth = 400, MaxWidth = double.PositiveInfinity, ClassName = "wide" }
                    )
            )
            .Children(new Control[]
            {

                new Border().Col(0).Row(0)
                    .ColSpan(2).RowSpan(3)
                    .Name("Pix2dCanvasContainer"),

#if DEBUG
                new AppMenuView().ColSpan(2),
#endif
                new TopBarView().Row(1).ColSpan(2)
                    .Margin(0, 0, 0, 1),

                new Border().Col(0).Row(2)
                    .BorderThickness(0, 0, 1, 0)
                    .Child(
                        new ScrollViewer()
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                            .Content(
                            new ToolBarView()
                                .HorizontalAlignment(HorizontalAlignment.Left)
                        )
                    ),

                new AdditionalTopBarView().Col(1).Row(2)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .HorizontalAlignment(HorizontalAlignment.Right),

                new InfoPanelView().Col(0).Row(3).ColSpan(2),

                new ActionsBarView().Col(1).Row(2)
                    .IsVisible(UiState.ShowExtraTools, bindingSource:UiState)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Top),

                new ClipboardActionsView().Col(1).Row(2)
                    .IsVisible(UiState.ShowClipboardBar, bindingSource : UiState)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Top),

                new TextBarView().Col(1).Row(2)
                    .IsVisible(UiState.ShowTextBar, bindingSource: UiState)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Top),

                //new RatePromptView().Col(1).Row(2),

                new TimeLineView().Col(1).Row(2)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .IsVisible(UiState.ShowTimeline, bindingSource:UiState),

                new LayersView().Col(1).Row(2)
                    .IsVisible(UiState.ShowLayers, bindingSource: UiState)
                    .Margin(0, 33, 0, 0)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .HorizontalAlignment(HorizontalAlignment.Right),

                new Canvas().Col(1).Row(2)
                    .Children(new Control[]
                    {

                        new PopupView()
                            .Header("Color edit")
                            .Top(10)
                            .Left(10)
                            .IsOpen(UiState.ShowColorEditor, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.ToggleColorEditorCommand)
                            .ShowPinButton(true)
                            .Content(new ColorPickerView()),

                        new PopupView()
                            .Header("Brush settings")
                            .IsOpen(UiState.ShowBrushSettings, bindingSource: UiState)
                            .CloseButtonCommand(Commands.View.ToggleBrushSettingsCommand)
                            .Width(210)
                            .Content(new BrushSettingsView()),

                        new PopupView()
                            .Header("Preview")
                            .IsOpen(UiState.ShowPreviewPanel, bindingSource : UiState)
                            .CloseButtonCommand(Commands.View.TogglePreviewPanelCommand)
                            .Top(40)
                            .Right(100)
                            .Content(new ArtworkPreviewView()),

                        new PopupView()
                            .Header("Image/Canvas size")
                            .IsOpen(UiState.ShowCanvasResizePanel, bindingSource : UiState)
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
                
                new ToolSettingsContainerView().Col(1).Row(2)
                    .IsVisible(UiState.ShowToolProperties, bindingSource : UiState)
                    .Margin(8, 120, 0, 0)
                    .MinWidth(40)
                    .MinHeight(40)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top),


                new PopupView()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
                    .Header("Export")
                    .IsOpen(UiState.ShowExportDialog, bindingSource: UiState)
                    .CloseButtonCommand(Commands.View.HideExportDialogCommand)
                    .Content(new ExportView()),

                new Border()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
                    .IsVisible(UiState.ShowMenu, bindingSource : UiState)
                    .Child(new MainMenuView()),

                new Border()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
                    .IsVisible(AppState.IsBusy, bindingSource : AppState)
                    .Background(StaticResources.Brushes.ModalOverlayBrush)
                    .Child(
                        new TextBlock()
                            .Text("Working...")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                    ),

                new DialogContainer()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
            });
}