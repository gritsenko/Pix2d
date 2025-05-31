using System.Globalization;
using Avalonia.Styling;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaNodes.Interactive;

namespace Pix2d.UI;

public class InfoPanelView : ComponentBase
{
    protected override StyleGroup? BuildStyles() =>
    [
        new Style<TextBlock>()
            .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
            .VerticalAlignment(VerticalAlignment.Center),

        new Style<TextBlock>(s=>s.Class("info-label"))
            .Opacity(0.3)
    ];

    protected override object Build() =>
        new BlurPanel()
            .IsHitTestVisible(false)
            .Height(24)
            .Padding(10, 4)
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        new TextBlock()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(() => SizeWidth),
                        new TextBlock().Classes("info-label")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text("\u00d7"),
                        new TextBlock()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(0, 0, 8, 0)
                            .Text(() => SizeHeight),

                        new TextBlock().Classes("info-label")
                            .Text("X:")
                            .Padding(8, 0, 0, 0),

                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(() => PointerInfoX),

                        new TextBlock().Classes("info-label")
                            .Text("Y:")
                            .Padding(8, 0, 0, 0),

                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(() => PointerInfoY)
                    )
            );

    [Inject] AppState AppState { get; } = null!;
    public SelectionState SelectionState => AppState.SelectionState;

    public string? PointerInfoX { get; set; } = "0";
    public string? PointerInfoY { get; set; } = "0";
    public string? SizeWidth { get; set; } = "0";
    public string? SizeHeight { get; set; } = "0";

    protected override void OnAfterInitialized()
    {
        SKInput.Current.PointerChanged += CurrentOnPointerChanged;
        SelectionState.WatchFor(x=>x.IsUserSelecting, UpdateSelectionInfo);
        SelectionState.WatchFor(x=>x.UserSelectingFrameSize, UpdateSelectionInfo);
    }

    private void CurrentOnPointerChanged(object? sender, SKInputPointer pointer)
    {
        var pos = pointer.WorldPosition;
        PointerInfoX = pos.X.ToString("N0");
        PointerInfoY = pos.Y.ToString("N0");
        StateHasChanged();
    }

    private void UpdateSelectionInfo()
    {
        var size = SelectionState.IsUserSelecting ? SelectionState.UserSelectingFrameSize : AppState.CurrentProject.SelectionSize;
        SizeWidth = size.Width.ToString(CultureInfo.InvariantCulture);
        SizeHeight = size.Height.ToString(CultureInfo.InvariantCulture);
        StateHasChanged();
    }
}