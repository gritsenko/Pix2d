using System.Globalization;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaNodes.Interactive;

namespace Pix2d.UI;

public partial class InfoPanelView(AppState appState) : ViewBase<InfoPanelView.State>(new State(appState))
{
    protected override StyleGroup? BuildStyles() =>
    [
        new Style<TextBlock>()
            .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
            .VerticalAlignment(VerticalAlignment.Center),

        new Style<TextBlock>(s=>s.Class("info-label"))
            .Opacity(0.3)
    ];

    protected override object Build(State state) =>
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
                            .Text(state, x => x.SizeWidth),
                        new TextBlock().Classes("info-label")
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text("\u00d7"),
                        new TextBlock()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(0, 0, 8, 0)
                            .Text(state, x => x.SizeHeight),

                        new TextBlock().Classes("info-label")
                            .Text("X:")
                            .Padding(8, 0, 0, 0),

                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(state, x => x.PointerInfoX),

                        new TextBlock().Classes("info-label")
                            .Text("Y:")
                            .Padding(8, 0, 0, 0),

                        new TextBlock().Col(1)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(state, x => x.PointerInfoY)
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;

        [ObservableProperty]
        public partial string PointerInfoX { get; set; } = "0";

        [ObservableProperty]
        public partial string PointerInfoY { get; set; } = "0";

        [ObservableProperty]
        public partial string SizeWidth { get; set; } = "0";

        [ObservableProperty]
        public partial string SizeHeight { get; set; } = "0";

        private SelectionState SelectionState => _appState.SelectionState;

        public State(AppState appState)
        {
            _appState = appState;

            SKInput.Current.PointerChanged += CurrentOnPointerChanged;
            SelectionState.WatchFor(x => x.IsUserSelecting, UpdateSelectionInfo);
            SelectionState.WatchFor(x => x.UserSelectingFrameSize, UpdateSelectionInfo);

            UpdateSelectionInfo();
        }

        private void CurrentOnPointerChanged(object? sender, SKInputPointer pointer)
        {
            var pos = pointer.WorldPosition;
            PointerInfoX = pos.X.ToString("N0");
            PointerInfoY = pos.Y.ToString("N0");
        }

        private void UpdateSelectionInfo()
        {
            var size = SelectionState.IsUserSelecting ? SelectionState.UserSelectingFrameSize : _appState.CurrentProject.SelectionSize;
            SizeWidth = size.Width.ToString(CultureInfo.InvariantCulture);
            SizeHeight = size.Height.ToString(CultureInfo.InvariantCulture);
        }
    }
}