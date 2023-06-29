using SkiaSharp;

namespace Pix2d.Views.ToolBar.Tools;

public class FillToolSettingsView : ComponentBase
{
    protected override object Build() =>
        new StackPanel()
            .Margin(8)
            .Children(
                new TextBlock()
                    .Text("Erase mode"),
                new ToggleSwitch()
                    .IsChecked(false)
            );

    private SKColor _lastColor;

    [Inject] IDrawingService DrawingService { get; set; } = null!;
    [Inject] AppState AppState { get; set; } = null!;
    public DrawingState DrawingState => AppState.DrawingState;

    public bool EraseMode { get; set; }

    private void SetEraseMode(bool value)
    {
        if (value)
        {
            _lastColor = DrawingState.CurrentColor;
            DrawingService.SetCurrentColor(SKColor.Empty);
        }
        else
            DrawingService.SetCurrentColor(_lastColor);
    }

    public void Activated()
    {
        _lastColor = DrawingState.CurrentColor;

        if (EraseMode)
        {
            DrawingService.SetCurrentColor(SKColor.Empty);
        }
    }

    public void Deactivated()
    {
        if (EraseMode)
            DrawingService.SetCurrentColor(_lastColor);
    }
}