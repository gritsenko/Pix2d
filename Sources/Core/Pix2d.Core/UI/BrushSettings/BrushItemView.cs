using Pix2d.Common.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.BrushSettings;

public partial class BrushItemView() : ViewBase<BrushItemView.State>(new State())
{
    private Primitives.Drawing.BrushSettings _preset = null!;

    protected override object Build(State state) =>
        new Border()
            .Background(state, x => x.PreviewBrush)
            .CornerRadius(StaticResources.Measures.ButtonCornerRadius)
            .Child(
                new TextBlock()
                    .IsVisible(state, x => x.ShowSizeText)
                    .Row(1)
                    .FontSize(9)
                    .Text(state, x => x.SizeText)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Padding(0, 0, 0, 1)
            );

    public Primitives.Drawing.BrushSettings Preset
    {
        get => _preset;
        set
        {
            _preset = value;
            ViewModel?.SetPreset(value);
        }
    }

    public bool ShowSizeText
    {
        get => ViewModel?.ShowSizeText ?? false;
        set => ViewModel!.ShowSizeText = value;
    }

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial bool ShowSizeText { get; set; }

        [ObservableProperty]
        public partial string SizeText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial IBrush PreviewBrush { get; set; } = StaticResources.Brushes.CheckerTilesBrush;

        public void SetPreset(Primitives.Drawing.BrushSettings preset)
        {
            SizeText = $"{preset.Scale}PX";
            PreviewBrush = preset.Brush?.GetPreviewBitmap(preset.Scale)?.ToBrush()?.Stretch(Stretch.None)
                ?? StaticResources.Brushes.CheckerTilesBrush;
        }
    }
}