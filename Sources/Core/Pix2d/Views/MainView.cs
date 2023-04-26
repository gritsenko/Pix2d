using System;
using Avalonia.Styling;
using Avalonia.Xaml.Interactions.Responsive;
using Pix2d.Shared;
using Pix2d.ViewModels;
using Pix2d.ViewModels.AppMenu;
using Pix2d.ViewModels.Layers;
using Pix2d.Views.Animation;
using Pix2d.Views.Layers;
using Pix2d.Views.MainMenu;
using Pix2d.Views.Text;
using Pix2d.Views.ToolBar;

namespace Pix2d.Views;

public class MainView : ViewBaseSingletonVm<MainViewModel>
{
    protected AppMenuViewModel AppMenuViewModel => GetViewModel<AppMenuViewModel>();

    protected Style AppMenuStyle => new(s => s.OfType<MenuItem>())
    {
        Setters =
        {
            new Setter(MenuItem.HeaderProperty, new Binding("Header")),
            new Setter(MenuItem.ItemsProperty, new Binding("MenuItems")),
            new Setter(MenuItem.CommandProperty, new Binding("Command"))
        }
    };

    protected override object Build(MainViewModel vm) =>
        new Grid()
            .Cols("Auto, *, Auto")
            .Rows("Auto, Auto, *, 32")
            .AddBehavior(
                new AdaptiveBehavior()
                    .Setters(
                        new AdaptiveClassSetter() {MinWidth = 0, MaxWidth = 400, ClassName = "small"},
                        new AdaptiveClassSetter()
                            {MinWidth = 400, MaxWidth = double.PositiveInfinity, ClassName = "wide"}
                    )
            )
            .Children(new Control[]
            {

                new Border().Col(0).Row(0)
                    .ColSpan(2).RowSpan(3)
                    .Name("Pix2dCanvasContainer"),

#if DEBUG
                new Menu().ColSpan(2)
                    .Background(StaticResources.Brushes.MainBackgroundBrush)
                    .Name("AppMenu")
                    .Padding(4)
                    .Foreground(Colors.White.ToBrush())
                    .Items(AppMenuViewModel.MenuItems)
                    .Styles(AppMenuStyle),
#endif
                new TopBarView().Row(1).ColSpan(2)
                    .Margin(0, 0, 0, 1),

                new Border().Col(0).Row(2)
                    .BorderThickness(0, 0, 1, 0)
                    .Child(
                        new ToolBarView()
                            .HorizontalAlignment(HorizontalAlignment.Left)
                    ),

                new AdditionalTopBarView().Col(1).Row(2)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .HorizontalAlignment(HorizontalAlignment.Right),

                new InfoPanelView().Col(0).Row(3).ColSpan(2),

                new ActionsBarView().Col(1).Row(2)
                    .IsVisible(@vm.ShowExtraTools)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Top),

                new ClipboardActionsView().Col(1).Row(2)
                    .IsVisible(@vm.ShowClipboardBar)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Top),

                new TextBarView().Col(1).Row(2)
                    .IsVisible(@vm.ShowTextBar, bindingSource: vm)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Top),

                new RatePromptView().Col(1).Row(2),

                new TimeLineView().Col(1).Row(2)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .IsVisible(Bind(@vm.ShowTimeline)),

                new LayersView().Col(1).Row(2)
                    .IsVisible(@vm.ShowLayers, bindingSource: vm)
                    .Margin(0, 33, 0, 0)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .HorizontalAlignment(HorizontalAlignment.Right),

                new ToolSettingsView().Col(1).Row(2)
                    .IsVisible(@vm.ShowToolProperties, bindingSource: vm)
                    .Margin(8, 120, 0, 0)
                    .MinWidth(40)
                    .MinHeight(40)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top),

                new Canvas().Col(1).Row(2)
                    .Children(new Control[]
                    {

                        new PopupView()
                            .Header("Color edit")
                            .Top(10)
                            .Left(10)
                            .IsOpen(@vm.ShowColorEditor)
                            .CloseButtonCommand(vm.ToggleColorEditorCommand)
                            .ShowPinButton(true)
                            .Content(new ColorPickerView()),

                        new PopupView()
                            .Header("Brush settings")
                            .IsOpen(@vm.ShowBrushSettings)
                            .CloseButtonCommand(vm.ToggleBrushSettingsCommand)
                            .Width(200)
                            .Content(new BrushSettingsView()),

                        new PopupView()
                            .Header("Preview")
                            .IsOpen(@vm.ShowPreviewPanel)
                            .CloseButtonCommand(vm.TogglePreviewPanelCommand)
                            .Top(40)
                            .Right(100)
                            .Content(new ArtworkPreviewView()),

                        new PopupView()
                            .Header("Image/Canvas size")
                            .IsOpen(@vm.ShowCanvasResizePanel)
                            .CloseButtonCommand(vm.ToggleCanavsSizePanelCommand)
                            .Width(220)
                            .Top(100)
                            .Right(100)
                            .Content(new ResizeCanvasView().Ref(out var _resizeCanvasView))
                            .OnShow(() => _resizeCanvasView.UpdateData()),

                        new PopupView()
                            .Header("Layer options")
                            .IsOpen(@vm.ShowLayerProperties)
                            .CloseButtonCommand(GetViewModel<LayersListViewModel>().CloseLayerPropertiesCommand)
                            .Width(220)
                            .Top(40)
                            .Right(100)
                            .Content(new LayerOptionsView())
                    }),

                new PopupView()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
                    .Header("Export")
                    .IsOpen(@vm.ShowExportDialog)
                    .CloseButtonCommand(vm.HideExportDialogCommand)
                    .Content(new ExportView()),

                new Border()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
                    .IsVisible(@vm.ShowMenu)
                    .Child(new MainMenuView()),

                new Border()
                    .Col(0).ColSpan(2)
                    .Row(0).RowSpan(3)
                    .IsVisible(@vm.IsBusy, BindingMode.OneWay)
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