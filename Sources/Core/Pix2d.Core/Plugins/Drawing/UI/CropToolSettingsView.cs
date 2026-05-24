using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.UI.Resources;

namespace Pix2d.Plugins.Drawing.UI;

/// <summary>
/// Top-bar UI for <see cref="CropTool"/>. Exposes Photoshop-style commit ✓ / cancel ✕ buttons against
/// the current crop frame. The buttons disable themselves when no frame is active so the user can
/// still drag out a new marquee without an inert toolbar in the way.
/// </summary>
public partial class CropToolSettingsView(IDrawingService drawingService) : ViewBase
{
    private readonly State _state = new(drawingService);

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                new Button()
                    .OnClick(_ => (DataContext as CropTool)?.CancelCrop())
                    .IsEnabled(_state, x => x.HasCropFrame)
                    .With(ButtonStyle)
                    .ToolTip_Tip("Cancel crop")
                    .Content("\xE711"),
                new Button()
                    .OnClick(_ => (DataContext as CropTool)?.ApplyCrop())
                    .IsEnabled(_state, x => x.HasCropFrame)
                    .With(ButtonStyle)
                    .ToolTip_Tip("Apply crop")
                    .Content("\xE73E")
            );

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _state.Subscribe();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _state.Unsubscribe();
    }

    private static void ButtonStyle(Button b)
    {
        b.Classes("btn")
            .Width(48)
            .Height(48)
            .FontSize(22)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));
    }

    public sealed partial class State : ObservableObject
    {
        private readonly IDrawingLayer _drawingLayer;

        [ObservableProperty]
        public partial bool HasCropFrame { get; set; }

        public State(IDrawingService drawingService)
        {
            _drawingLayer = drawingService.DrawingLayer;
            SyncFromDrawingLayer();
        }

        public void Subscribe()
        {
            _drawingLayer.PixelsSelected += DrawingLayerOnSelectionChanged;
            _drawingLayer.SelectionRemoved += DrawingLayerOnSelectionChanged;
            SyncFromDrawingLayer();
        }

        public void Unsubscribe()
        {
            _drawingLayer.PixelsSelected -= DrawingLayerOnSelectionChanged;
            _drawingLayer.SelectionRemoved -= DrawingLayerOnSelectionChanged;
        }

        private void DrawingLayerOnSelectionChanged(object? sender, EventArgs e) => SyncFromDrawingLayer();

        private void SyncFromDrawingLayer() => HasCropFrame = _drawingLayer.HasSelection;
    }
}
