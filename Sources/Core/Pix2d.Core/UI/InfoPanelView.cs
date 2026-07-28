using System.Globalization;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Mvvm.Messaging;
using Pix2d.Abstract.Services;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.UI;

public partial class InfoPanelView(AppState appState, IMessenger messenger, ISelectionService selectionService) : ViewBase<InfoPanelView.State>(new State(appState, messenger, selectionService))
{
    protected override StyleGroup? BuildStyles() =>
    [
        // Figma "Body 11": Zed Mono Extended 11px/16px line, 60% white.
        new Style<TextBlock>()
            .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
            .FontSize(11)
            .LineHeight(16)
            .Foreground(StaticResources.Brushes.SecondaryForegroundBrush)
            .VerticalAlignment(VerticalAlignment.Center),

        // 0.5 on top of the 60% foreground ≈ the design's 30% muted tier.
        new Style<TextBlock>(s=>s.Class("info-label"))
            .Opacity(0.5),

        // Mode chip: accent-filled pill, dark text (the accent is a light orange→amber gradient).
        new Style<TextBlock>(s => s.Class("mode-chip-text"))
            .FontSize(10)
            .LineHeight(12)
            .Foreground(StaticResources.Brushes.MainBackgroundBrush)
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

                        // Edit-context indicator. Sprite is the default and stays unmarked; a non-default
                        // context announces itself here (accent pill) so it is obvious why the toolbars and
                        // the pointer behaviour changed. Collapses entirely in Sprite context.
                        new Border()
                            .IsVisible(state, x => x.ShowModeChip)
                            .Background(StaticResources.Brushes.AccentBrush)
                            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius / 2.0)
                            .Padding(6, 1)
                            .Margin(0, 0, 8, 0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Child(
                                new TextBlock()
                                    .Classes("mode-chip-text")
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Text(state, x => x.ModeLabel)),

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
        private readonly IMessenger _messenger;
        private readonly ISelectionService _selectionService;

        [ObservableProperty]
        public partial string PointerInfoX { get; set; } = "0";

        [ObservableProperty]
        public partial string PointerInfoY { get; set; } = "0";

        [ObservableProperty]
        public partial string SizeWidth { get; set; } = "0";

        [ObservableProperty]
        public partial string SizeHeight { get; set; } = "0";

        [ObservableProperty]
        public partial bool ShowModeChip { get; set; }

        [ObservableProperty]
        public partial string ModeLabel { get; set; } = "";

        private SelectionState SelectionState => _appState.SelectionState;

        public State(AppState appState, IMessenger messenger, ISelectionService selectionService)
        {
            _appState = appState;
            _messenger = messenger;
            _selectionService = selectionService;

            SKInput.Current.PointerChanged += CurrentOnPointerChanged;
            SelectionState.WatchFor(x => x.IsUserSelecting, UpdateSelectionInfo);
            SelectionState.WatchFor(x => x.UserSelectingFrameSize, UpdateSelectionInfo);
            _messenger.Register<NodesSelectedMessage>(this, _ => UpdateSelectionInfo());
            _messenger.Register<OperationInvokedMessage>(this, _ => UpdateSelectionInfo());
            _messenger.Register<CanvasSizeChangedMessage>(this, _ => UpdateSelectionInfo());
            _messenger.Register<DrawingTargetChangedMessage>(this, _ => UpdateSelectionInfo());
            _appState.WatchFor(x => x.CurrentProject, UpdateSelectionInfo);
            // WatchForCurrentProject (not WatchFor) so the binding survives a project-tab switch.
            _appState.WatchForCurrentProject(x => x.CurrentContextType, UpdateModeInfo);

            UpdateSelectionInfo();
            UpdateModeInfo();
        }

        private void UpdateModeInfo()
        {
            var context = _appState.CurrentProject.CurrentContextType;

            // Sprite is the default context — no chip, so the common case stays quiet.
            ModeLabel = context switch
            {
                EditContextType.General => L("Layout mode").ToUpperInvariant(),
                EditContextType.General3d => L("3D mode").ToUpperInvariant(),
                EditContextType.Text => L("Text mode").ToUpperInvariant(),
                _ => ""
            };
            ShowModeChip = ModeLabel.Length > 0;
        }

        private void CurrentOnPointerChanged(object? sender, SKInputPointer pointer)
        {
            var pos = pointer.WorldPosition;
            PointerInfoX = pos.X.ToString("N0");
            PointerInfoY = pos.Y.ToString("N0");
        }

        private void UpdateSelectionInfo()
        {
            var size = GetDisplaySize();
            SizeWidth = MathF.Round(size.Width).ToString(CultureInfo.InvariantCulture);
            SizeHeight = MathF.Round(size.Height).ToString(CultureInfo.InvariantCulture);
        }

        private SKSize GetDisplaySize()
        {
            if (SelectionState.IsUserSelecting)
                return SelectionState.UserSelectingFrameSize;

            var project = _appState.CurrentProject;
            if (project.HasSelection)
                return project.SelectionSize;

            if (project.SceneNode != null)
            {
                var container = _selectionService.GetActiveContainer();
                if (container != null && container.Size.Width > 0 && container.Size.Height > 0)
                    return container.Size;
            }

            if (project.CurrentEditedNode != null && project.CurrentEditedNode.Size.Width > 0 && project.CurrentEditedNode.Size.Height > 0)
                return project.CurrentEditedNode.Size;

            return project.SelectionSize;
        }
    }
}