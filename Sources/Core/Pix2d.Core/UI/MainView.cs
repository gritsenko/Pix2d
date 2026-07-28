using System.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.VisualTree;
using SkiaSharp;
using Pix2d.UI.Animation;
using Pix2d.UI.BrushSettings;
using Pix2d.UI.Export;
using Pix2d.UI.Layers;
using Pix2d.UI.MainMenu;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using Pix2d.UI.ToolBar;
using System.ComponentModel;
using System.Diagnostics;

namespace Pix2d.UI;

public partial class MainView : ViewBase<MainViewModel>
{
    public MainView(
        AppState appState,
        IDialogService dialogService,
        IMessenger messenger,
        ICommandService commandService,
        IImportFlowService importFlowService)
        : base(new MainViewModel(appState, dialogService, messenger, commandService, importFlowService))
    {
    }

    protected override StyleGroup BuildStyles() =>
    [
        new Style<TimeLineView>(s => s.Name("timeLine"))
            .RenderTransform(TransformOperations.Parse("translateY(30px)"))
            .IsVisible(false),
        new Style<TimeLineView>(s => s.Name("timeLine").Class("shown"))
            .RenderTransform(TransformOperations.Parse("translateY(0)"))
            .IsVisible(true),

        new Style<ToolBarView>()
            .Margin(StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, 48)
            .Col(0)
            .Row(1)
            .RowSpan(2)
            // Alignment as a base style (not local in Build) so Narrow can stretch it full width.
            .HorizontalAlignment(HorizontalAlignment.Left)
            .VerticalAlignment(VerticalAlignment.Center),

        new Style<LayersView>()
            .Margin(0, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin)
            .VerticalAlignment(VerticalAlignment.Center)
            .RowSpan(1),

        new Style<AdditionalTopBarView>()
            .Row(2)
            .Margin(0, 0, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin),

        new Style<ZoomPanelView>()
            .Col(0).ColSpan(3).Row(2)
            .Margin(StaticResources.Measures.PanelMargin)
            .HorizontalAlignment(HorizontalAlignment.Center),

        new Style<Canvas>(s => s.Name("PopupContainer"))
            .Col(1)
            .ColSpan(2)
            .Row(1)
            .RowSpan(2),

        new Style<ToolGroupContainerView>().Col(1).Row(1)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .VerticalAlignment(VerticalAlignment.Center)
            .Margin(8, 0, 120, 0),

        new StyleGroup(_ => VisualStates.Narrow())
        {

            new Style<LayersView>()
                .Margin(0, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin + 56 + 24 + 56)
                .VerticalAlignment(VerticalAlignment.Bottom)
                .RowSpan(2),

            new Style<ToolBarView>()
                .Margin(0)
                .Col(0)
                .Row(2)
                .RowSpan(1)
                .ColSpan(4)
                // Full window width, anchored to the bottom edge.
                .HorizontalAlignment(HorizontalAlignment.Stretch)
                .VerticalAlignment(VerticalAlignment.Bottom),

            new Style<InfoPanelView>()
                .IsVisible(false),

            new Style<ZoomPanelView>()
                .Col(0).ColSpan(1)
                .VerticalAlignment(VerticalAlignment.Bottom)
                // Bottom edge on the shared line just above the flush tools bar; left cleared past the color/brush panel.
                .Margin(StaticResources.Measures.PanelMargin * 2 + 56, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, NarrowBottomBarOffset),

            new Style<AdditionalTopBarView>()
                .Margin(0, StaticResources.Measures.PanelMargin, StaticResources.Measures.PanelMargin, NarrowBottomBarOffset),

            new Style<Canvas>(s => s.Name("PopupContainer"))
                .Col(0)
                .ColSpan(3)
                .RowSpan(2),

            new Style<ToolGroupContainerView>()
                .Col(0)
                .ColSpan(3)
                .Row(2)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Top)
                .Margin(StaticResources.Measures.PanelMargin, 0, StaticResources.Measures.PanelMargin, 0)
        }
    ];

    protected override object Build(MainViewModel vm) =>
        new Grid().Name("RootGrid").Rows("Auto, *").Children(
            // Desktop-only project tab strip; it sits in its own row above the canvas so it never
            // overlaps it (gated by IPlatformStuffService.SupportsMultipleProjects inside the view).
            ViewFactory.Create<ProjectTabsView>().Row(0),

            new Border()
                .Name("Pix2dCanvasContainer")
                .Row(1)
                .OnPointerPressed(e =>
                {
                    if (e.Source is StyledElement element)
                        vm.NotifyWindowClicked(element);
                }, RoutingStrategies.Tunnel),

            new LayoutTransformControl()
                .Ref(out _layoutTransformControl)
                .Name("LayoutTransformControl")
                .Row(1)
                .Child(
                    new Grid()
                        .Name("UiGrid")
                        .Ref(out _rootGrid)
                        .Cols("Auto, *, Auto")
                        .Rows("Auto, *, Auto, Auto")
                        .Children([
                            ViewFactory.Create<TopBarView>().Ref(out _topBarView).Row(0).ColSpan(3)
                                .Margin(0, 0, 0, 1),

                            ViewFactory.Create<ToolBarView>(),

                            ViewFactory.Create<AdditionalTopBarView>().Col(2),

                            //ViewFactory.Create<RatePromptView>().Col(0).ColSpan(3).Row(2)
                            //    .IsVisible(state, x => x.ShowRatePrompt),

                            ViewFactory.Create<InfoPanelView>().Col(0).Row(2).ColSpan(2)
                                .Margin(StaticResources.Measures.PanelMargin)
                                .HorizontalAlignment(HorizontalAlignment.Left)
                                .VerticalAlignment(VerticalAlignment.Bottom),

                            ViewFactory.Create<ZoomPanelView>(),

                            // Full-width top strip: action / tool bars (Row 0-1) with the notification
                            // zone directly under them (Row 2). The ActionsBar and TopToolUiContainer are
                            // horizontal scroll hosts whose width is clamped to the window
                            // (ClampMaxWidthToViewport) so they scroll instead of overflowing on a narrow
                            // screen — the grid column can't bound them because the bottom side-panels
                            // contaminate the Auto columns' width.
                            new Grid().Col(0).ColSpan(3).Row(1).Rows("auto,auto,auto")
                                .Margin(StaticResources.Measures.PanelMargin)
                                .Children(
                                    // Three mutually exclusive bars share this slot, each self-gating:
                                    // Sprite context extra tools, the General (objects) action bar, and the
                                    // Apply/Cancel bar of an artboard canvas-edit session.
                                    ViewFactory.Create<ActionsBarView>()
                                        .IsVisible(vm, x => x.ShowSpriteExtraTools)
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                        .VerticalAlignment(VerticalAlignment.Top),

                                    ViewFactory.Create<ObjectActionsBarView>()
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                        .VerticalAlignment(VerticalAlignment.Top),

                                    ViewFactory.Create<ArtboardCanvasEditView>()
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                        .VerticalAlignment(VerticalAlignment.Top),

                                    ViewFactory.Create<TopToolUiContainer>().Row(1)
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                        .VerticalAlignment(VerticalAlignment.Top),

                                    // Notification cards (crash-recovery / rate prompt), directly under the
                                    // bars. Each is a responsive card (notify-card / notify-content styles):
                                    // centred + one-row on desktop, stretched + actions-on-a-new-line on a
                                    // narrow screen. Width is clamped to the window so the card wraps its
                                    // text instead of forcing the grid wider than the viewport. Both are
                                    // IsVisible-gated and mutually exclusive in practice, so they share Row 2.
                                    new Panel().Row(2)
                                        .Margin(0, StaticResources.Measures.PanelMargin, 0, 0)
                                        .Children(
                                            new Border().Name("RecoveryNotice").Classes("notify-card")
                                                .IsVisible(vm, x => x.ShowRecoveryNotice)
                                                .ClampMaxWidthToViewport(StaticResources.Measures.PanelMargin * 2)
                                                .Padding(16, 10)
                                                .Child(new StackPanel().Classes("notify-content").Spacing(8)
                                                    .Children(
                                                        new TextBlock().Classes("body14")
                                                            .MaxWidth(320)
                                                            .VerticalAlignment(VerticalAlignment.Center)
                                                            .TextWrapping(TextWrapping.Wrap)
                                                            .Text(L("Recovered your unsaved work after an unexpected close.")),
                                                        new Button().Classes("btn")
                                                            .VerticalAlignment(VerticalAlignment.Center)
                                                            .HorizontalAlignment(HorizontalAlignment.Right)
                                                            .Content(L("Dismiss"))
                                                            .OnClick(_ => vm.DismissRecoveryNotice()))),

                                            new Border().Classes("notify-card")
                                                .IsVisible(vm, x => x.ShowRatePrompt)
                                                .ClampMaxWidthToViewport(StaticResources.Measures.PanelMargin * 2)
                                                .Padding(16, 10)
                                                .Child(ViewFactory.Create<RatePromptView>())
                                )),

                            ViewFactory.Create<TimeLineView>()
                                .With(v => v.Transitions = new Transitions
                                {
                                    new TransformOperationsTransition
                                    {
                                        Property = TimeLineView.RenderTransformProperty,
                                        Duration = TimeSpan.FromSeconds(0.3),
                                        Easing = new BackEaseOut()
                                    }
                                })
                                .Ref(out _timeLineView)
                                .Col(0).Row(3).Name("timeLine")
                                .ColSpan(3)
                                .VerticalAlignment(VerticalAlignment.Bottom),

                            ViewFactory.Create<LayersView>().Col(2).Row(1)
                                .IsVisible(vm, x => x.ShowLayers)
                                .HorizontalAlignment(HorizontalAlignment.Right),

                            new Canvas().Name("PopupContainer")
                                .Ref(out _panelsContainer)
                                .Children(new Control[]
                                {
                                    ViewFactory.Create<PopupView>().Name("ColorPicker")
                                        .Ref(out _colorPickerPopup)
                                        .Header(L("Color"))
                                        .Canvas_Top(10)
                                        .Canvas_Left(10)
                                        .UseCenteredPositionOnNarrowScreen(true)
                                        .IsOpen(vm, x => x.ShowColorEditor, BindingMode.TwoWay)
                                        .CloseButtonCommand(vm.ViewCommands.ToggleColorEditorCommand)
                                        .ShowPinButton(true)
                                        .Content(ViewFactory.Create<ColorPickerView>()),

                                    ViewFactory.Create<PopupView>().Name("BrushSettings")
                                        .Ref(out _brushSettingsPopup)
                                        .Header(L("Brush"))
                                        .IsOpen(vm, x => x.ShowBrushSettings, BindingMode.TwoWay)
                                        .CloseButtonCommand(vm.ViewCommands.ToggleBrushSettingsCommand)
                                        .Width(280)
                                        .UseCenteredPositionOnNarrowScreen(true)
                                        .ShowPinButton(true)
                                        .Content(ViewFactory.Create<BrushSettingsView>()),

                                    ViewFactory.Create<PopupView>().Name("ArtworkPreview")
                                        .Ref(out _artworkPreviewPopup)
                                        .Header(L("Preview"))
                                        .IsOpen(vm, x => x.ShowPreviewPanel, BindingMode.TwoWay)
                                        .CloseButtonCommand(vm.ViewCommands.TogglePreviewPanelCommand)
                                        .Canvas_Top(40)
                                        .Canvas_Right(100)
                                        .UseCenteredPositionOnNarrowScreen(true)
                                        .Content(ViewFactory.Create<ArtworkPreviewView>()),

                                    ViewFactory.Create<PopupView>()
                                        .Ref(out _resizeCanvasPopup)
                                        .Header(L("Image/Canvas size"))
                                        .IsOpen(vm, x => x.ShowCanvasResizePanel, BindingMode.TwoWay)
                                        .CloseButtonCommand(vm.ViewCommands.ToggleCanvasSizePanelCommand)
                                        .Width(220)
                                        .Canvas_Top(100)
                                        .Canvas_Right(100)
                                        .UseCenteredPositionOnNarrowScreen(true)
                                        .Content(ViewFactory.Create<ResizeCanvasView>().Ref(out var resizeCanvasView))
                                        .OnShow(() => resizeCanvasView.UpdateData()),

                                    ViewFactory.Create<PopupView>()
                                        .Ref(out _layerOptionsPopup)
                                        .Header(L("Layer options"))
                                        .IsOpen(vm, x => x.ShowLayerProperties, BindingMode.TwoWay)
                                        .CloseButtonCommand(vm.ViewCommands.HideLayerOptionsCommand)
                                        .Width(300)
                                        .Canvas_Top(40)
                                        .Canvas_Right(120)
                                        .UseCenteredPositionOnNarrowScreen(true)
                                        .Content(ViewFactory.Create<LayerOptionsView>())
                                }),

                            ViewFactory.Create<ToolGroupContainerView>()
                                .IsVisible(vm, x => x.ShowToolGroup)
                                .MinWidth(40)
                                .MinHeight(40),

                            ViewFactory.Create<ExportView>().ColSpan(3).RowSpan(4)
                                .IsVisible(vm, x => x.ShowExportDialog),

                            new Border().Name("MainMenuContainer")
                                .Col(0).ColSpan(3)
                                .Row(0).RowSpan(4)
                                .IsVisible(vm, x => x.ShowMenu)
                                .Child(ViewFactory.Create<MainMenuView>()),

                            new Border().Name("LoadingOverlay")
                                .Col(0).ColSpan(3)
                                .Row(0).RowSpan(3)
                                .IsVisible(vm, x => x.IsBusy)
                                .Background(StaticResources.Brushes.ModalOverlayBrush)
                                .Child(
                                    new TextBlock()
                                        .Text(L("Working..."))
                                        .VerticalAlignment(VerticalAlignment.Center)
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                )
                        ])
                ),

            ViewFactory.Create<DialogContainer>().RowSpan(2)
        );

    // Narrow-mode: the floating bottom panels (zoom, additional bar, color/brush) share one bottom line
    // that clears the full-width flush tools bar — its compact tool buttons plus their margins — with a
    // panel-margin gap above it. Keeping this derived (not a magic "56") keeps the row aligned if the
    // compact button metrics change.
    private static readonly double NarrowBottomBarOffset =
        StaticResources.Measures.CompactToolButtonSize
        + StaticResources.Measures.CompactButtonMargin * 2
        + StaticResources.Measures.PanelMargin;

    private Canvas _panelsContainer = null!;
    private Grid _rootGrid = null!;
    private TopBarView _topBarView = null!;
    private TimeLineView _timeLineView = null!;
    private LayoutTransformControl _layoutTransformControl = null!;
    private PopupView _colorPickerPopup = null!;
    private PopupView _brushSettingsPopup = null!;
    private PopupView _artworkPreviewPopup = null!;
    private PopupView _resizeCanvasPopup = null!;
    private PopupView _layerOptionsPopup = null!;

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

        ViewModel!.InitializePanelsContainer(_panelsContainer);
        ViewModel!.PropertyChanged += OnViewModelPropertyChanged;

        if (ViewModel.UiScale != 1)
        {
            _layoutTransformControl.LayoutTransform = new Avalonia.Media.ScaleTransform(ViewModel.UiScale, ViewModel.UiScale);
        }

        UpdateResponsiveLayout();
        UpdateTimelineVisibility();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (ViewModel != null)
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        UpdateResponsiveLayout();
        RepositionFloatingPanels();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ShowTimeline))
            UpdateTimelineVisibility();
    }

    private void UpdateResponsiveLayout()
    {
        if (_rootGrid == null)
            return;

        var isNarrow = Bounds.Width <= 500;
        _rootGrid.Classes.Set(nameof(VisualStates.Narrow), isNarrow);
        _rootGrid.Classes.Set(nameof(VisualStates.Wide), !isNarrow);
        ViewModel?.UpdateResponsiveLayout(Bounds.Width);
    }

    private void UpdateTimelineVisibility()
    {
        if (_timeLineView == null || ViewModel == null)
            return;

        _timeLineView.Classes.Set("shown", ViewModel.ShowTimeline);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var hasFiles = e.DataTransfer.Formats.Contains(DataFormat.File);
        if (hasFiles)
            e.DragEffects = DragDropEffects.Copy;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Handled)
            return;

        var droppedFiles = e.DataTransfer.TryGetFiles();
        if (droppedFiles == null)
            return;

        // Convert the drop point to world coordinates via the canvas (same path the pointer pipeline
        // uses). Used to decide whether a still image lands in the current sprite or in a new one.
        SKPoint? dropWorldPosition = null;
        var canvas = this.GetVisualDescendants().OfType<SkiaCanvas>().FirstOrDefault();
        if (canvas != null)
            dropWorldPosition = canvas.GetWorldPosition(e.GetPosition(canvas));

        await ViewModel!.HandleDropAsync(droppedFiles, dropWorldPosition);
    }

    private void RepositionFloatingPanels()
    {
        _colorPickerPopup?.ResetPositionForCurrentLayout();
        _brushSettingsPopup?.ResetPositionForCurrentLayout();
        _artworkPreviewPopup?.ResetPositionForCurrentLayout();
        _resizeCanvasPopup?.ResetPositionForCurrentLayout();
        _layerOptionsPopup?.ResetPositionForCurrentLayout();
    }
}