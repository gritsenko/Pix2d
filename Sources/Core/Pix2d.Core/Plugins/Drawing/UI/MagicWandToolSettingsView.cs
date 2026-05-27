using Avalonia.Data;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Drawing;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.Drawing.UI;

public partial class MagicWandToolSettingsView(ICommandService commandService, IDrawingService drawingService) : ViewBase
{
    private readonly State _state = new(commandService, drawingService);

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                // ViewFactory.Create<MagicWandClipboardActionsView>(),
                new Border()
                    .Padding(new Thickness(14, 8))
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
                    .BorderThickness(new Thickness(1))
                    .CornerRadius(new CornerRadius(12))
                    .Child(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Spacing(12)
                            .Children(
                                new TextBlock()
                                    .Width(84)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .FontSize(12)
                                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                                    .Text("TOLERANCE"),
                                new Slider()
                                    .Width(160)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Minimum(0)
                                    .Maximum(255)
                                    .TickFrequency(1)
                                    .IsSnapToTickEnabled(true)
                                    .SmallChange(1)
                                    .LargeChange(10)
                                    .Value(_state, x => x.Tolerance, BindingMode.TwoWay),
                                new TextBlock()
                                    .Width(28)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                                    .Text(_state, x => x.ToleranceText),
                                new ToggleSwitch()
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .OffContent("Connected")
                                    .OnContent("Whole layer")
                                    .IsChecked(_state, x => x.SelectWholeLayer, BindingMode.TwoWay)
                            )
                    )
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
            _state.SetTool(change.NewValue as PixelSelectColorTool);
    }

    private static void ButtonStyle(Button b)
    {
        b.Classes("btn")
            .Width(48)
            .Height(48)
            .FontSize(18)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
            b.ToolTip_Tip(pc.Tooltip);
    }

    public sealed partial class State : ObservableObject
    {
        private readonly IDrawingLayer _drawingLayer;
        private PixelSelectColorTool? _tool;
        private bool _isSyncing;

        [ObservableProperty]
        public partial bool CanTransformSelection { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ToleranceText))]
        public partial double Tolerance { get; set; }

        [ObservableProperty]
        public partial bool SelectWholeLayer { get; set; }

        public State(ICommandService commandService, IDrawingService drawingService)
        {
            _drawingLayer = drawingService.DrawingLayer;
            SpriteEditCommands = commandService.GetCommandList<ISpriteEditCommands>() ??
                throw new InvalidOperationException("CommandService is not available");

            SyncFromDrawingLayer();
        }

        public ISpriteEditCommands SpriteEditCommands { get; }

        public string ToleranceText => ((int)Math.Round(Tolerance)).ToString();

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

        public void SetTool(PixelSelectColorTool? tool)
        {
            _tool = tool;

            _isSyncing = true;
            Tolerance = tool?.Tolerance ?? 0;
            SelectWholeLayer = tool?.SelectWholeLayer ?? false;
            _isSyncing = false;
        }

        partial void OnToleranceChanged(double value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.Tolerance = (int)Math.Round(value);
        }

        partial void OnSelectWholeLayerChanged(bool value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.SelectWholeLayer = value;
        }

        private void DrawingLayerOnSelectionChanged(object? sender, EventArgs e) => SyncFromDrawingLayer();

        private void SyncFromDrawingLayer()
        {
            CanTransformSelection = _drawingLayer.HasSelection;
        }
    }
}
