using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Drawing;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.Plugins.Drawing.UI;

public partial class SelectionToolSettingsView(ICommandService commandService, IDrawingService drawingService) : ViewBase
{
    private readonly State _state = new(commandService, drawingService);

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                new Button()
                    .Command(_state.SpriteEditCommands.ActivateSelectionTransform)
                    .IsEnabled(_state, x => x.CanTransformSelection)
                    .With(ButtonStyle)
                    .Content(
                        new Path()
                            .Data(StaticResources.Icons.SelectionTransformIcon)
                            .Stretch(Stretch.Uniform)
                            .Width(18)
                            .Height(18)
                            .Fill(StaticResources.Brushes.ForegroundBrush)),
                ViewFactory.Create<ClipboardActionsView>());

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
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
            b.ToolTip_Tip(pc.Tooltip);
    }

    public sealed partial class State : ObservableObject
    {
        private readonly IDrawingLayer _drawingLayer;

        [ObservableProperty]
        public partial bool CanTransformSelection { get; set; }

        public State(ICommandService commandService, IDrawingService drawingService)
        {
            _drawingLayer = drawingService.DrawingLayer;
            SpriteEditCommands = commandService.GetCommandList<ISpriteEditCommands>() ??
                throw new InvalidOperationException("CommandService is not available");

            SyncFromDrawingLayer();
        }

        public ISpriteEditCommands SpriteEditCommands { get; }

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

        private void SyncFromDrawingLayer()
        {
            CanTransformSelection = _drawingLayer.HasSelection;
        }
    }
}
