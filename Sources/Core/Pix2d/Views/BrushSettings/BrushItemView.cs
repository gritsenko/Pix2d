using Avalonia.Controls.Shapes;
using SkiaSharp;

namespace Pix2d.Views.BrushSettings;

public class BrushItemView : ComponentBase
{

    protected override object Build() =>
        new Grid()
            .Rows("*,Auto")
            .Children(
                new Rectangle().Fill(() => Preview.ToBrush().Stretch(Stretch.None)),
                new TextBlock()
                    .IsVisible(() => ShowSizeText)
                    .Row(1)
                    .FontSize(10)
                    .Text(() => $"{Preset?.Scale}px")
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Padding(0, 0, 0, 1)
            );


    private Primitives.Drawing.BrushSettings _preset;
    private bool _showSizeText;

    public Primitives.Drawing.BrushSettings Preset
    {
        get => _preset;
        set
        {
            _preset = value;
            StateHasChanged();
        }
    }

    public SKBitmap Preview => Preset?.Brush?.GetPreview(Preset.Scale) ?? null;

    public bool ShowSizeText
    {
        get => _showSizeText;
        set
        {
            _showSizeText = value;
            StateHasChanged();
        }
    }
}