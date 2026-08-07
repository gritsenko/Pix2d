using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Common.Extensions;
using Pix2d.CommonNodes;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.Layers;

public partial class LayerItemView : ViewBase<LayerItemView.State>
{
    private static readonly IControlTemplate ButtonTemplate =
        new FuncControlTemplate<Button>((button, _) => new ContentPresenter { Content = button.Content, Background = StaticResources.Brushes.CheckerTilesBrush });

    public LayerItemView(LayerItemViewModel viewModel, IViewPortRefreshService viewPortRefreshService)
        : base(new State(viewModel, viewPortRefreshService))
    {
    }

    protected override object Build(State state) =>
        new Border()
            .CornerRadius(6)
            .ClipToBounds(true)
            .Child(
                // Thumbnail block on top, name strip below it — deliberately NOT overlaid: the icon
                // column needs the full 80px for its three rows, and insetting it by a strip's height
                // clips the 18pt glyphs.
                new Grid()
                    .Rows("80,Auto")
                    .Width(80)
                    .Children(
                        new Grid()
                            .Margin(0)
                            .Width(80)
                            .Height(80)
                            .Children(
                                new Button()
                                    .Padding(0)
                                    .OnClick(_ => OnThumbnailClick())
                                    .Template(ButtonTemplate)
                                    .Content(
                                        new Rectangle()
                                            .Width(100)
                                            .Height(100)
                                            .Fill(state, x => x.PreviewBrush))
                                    .OnPointerPressed(OnThumbnailPointerPressed),
                                new Grid()
                                    .Rows("*,*,*")
                                    .Background("#66363D45".ToColor().ToBrush())
                                    .Width(32)
                                    .HorizontalAlignment(HorizontalAlignment.Left)
                                    .Children(
                                        new Button().Name("ToggleVisibilityButton")
                                            .Row(0)
                                            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                            .FontSize(18)
                                            .OnClick(_ => state.ToggleVisibility())
                                            .Foreground(state, x => x.VisibilityForeground)
                                            .Content("\xe92a"),
                                        new Button().Name("LockTransparentPixelsButton")
                                            .Row(1)
                                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                                            .FontSize(18)
                                            .IsVisible(state, x => x.ShowColorLockButton)
                                            .OnClick(_ => state.ToggleColorLock(LeftPointerPressed))
                                            .Foreground(state, x => x.ColorLockForeground)
                                            .Content("\xe901"),
                                        new Button()
                                            .Row(2)
                                            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                            .FontSize(18)
                                            .IsVisible(state, x => x.HasEffects)
                                            .OnClick(_ => state.ToggleEffects())
                                            .Foreground(state, x => x.EffectsForeground)
                                            .Content("\xe939")
                                    ),
                                // Blend-mode marker keeps its old spot over the artwork rather than
                                // sharing the strip: a word like "Luminosity" would leave the title
                                // ellipsized to two characters, and the title is the point of the strip.
                                new Grid()
                                    .VerticalAlignment(VerticalAlignment.Bottom)
                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                    .IsVisible(state, x => x.ShowBlendModeName)
                                    .Children(
                                        new TextBlock().Text(state, x => x.BlendModeText).Foreground(Brushes.White)
                                            .Margin(8),
                                        new TextBlock().Text(state, x => x.BlendModeText).Foreground(Brushes.Black)
                                            .Margin(7)
                                    )
                            ),
                        // Present only when the layer has a user-given title. An auto-generated
                        // "Layer 003" is not a name — it buys the tile nothing, so the strip collapses
                        // and the tile keeps its original 80x80.
                        new TextBlock()
                            .Row(1)
                            .Height(NameStripHeight)
                            .IsVisible(state, x => x.ShowNameStrip)
                            .Background("#66363D45".ToColor().ToBrush())
                            .Text(state, x => x.LayerName)
                            .ToolTip_Tip(state, x => x.LayerName)
                            .FontSize(10)
                            .Foreground(Brushes.White)
                            .TextTrimming(TextTrimming.CharacterEllipsis)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Padding(4, 0, 4, 0)
                    )
            );

    private const int NameStripHeight = 16;

    public Action? LeftPointerPressed { get; set; }

    public Action? RightPointerPressed { get; set; }

    /// <summary>Ctrl+click (Cmd on macOS) on the thumbnail — load the layer's opaque pixels as a selection.</summary>
    public Action? ModifiedLeftPointerPressed { get; set; }

    private bool _pressedWithSelectionModifier;

    private void OnThumbnailPointerPressed(PointerPressedEventArgs e)
    {
        // Click fires on release and RoutedEventArgs carries no modifiers, so the state is recorded
        // here — the modifier held when the gesture *started* is the one that decides what it means.
        _pressedWithSelectionModifier =
            e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            RightPointerPressed?.Invoke();
        }
    }

    private void OnThumbnailClick()
    {
        if (_pressedWithSelectionModifier)
        {
            _pressedWithSelectionModifier = false;
            // Deliberately does NOT select the layer: the point of the gesture is to mask what you are
            // already drawing on with another layer's silhouette (same as Photoshop).
            ModifiedLeftPointerPressed?.Invoke();
            return;
        }

        LeftPointerPressed?.Invoke();
    }

    public sealed partial class State : ObservableObject
    {
        private readonly IViewPortRefreshService _viewPortRefreshService;

        public State(LayerItemViewModel layer, IViewPortRefreshService viewPortRefreshService)
        {
            _viewPortRefreshService = viewPortRefreshService;
            Layer = layer;
            Layer.Invalidated += SyncFromModel;
            SyncFromModel();
        }

        public LayerItemViewModel Layer { get; }

        [ObservableProperty]
        public partial IBrush PreviewBrush { get; set; } = Brushes.Transparent;

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        [ObservableProperty]
        public partial bool ShowColorLockButton { get; set; }

        [ObservableProperty]
        public partial bool HasEffects { get; set; }

        [ObservableProperty]
        public partial bool ShowBlendModeName { get; set; }

        [ObservableProperty]
        public partial string LayerName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool ShowNameStrip { get; set; }

        [ObservableProperty]
        public partial string BlendModeText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial IBrush VisibilityForeground { get; set; } = Brushes.Gray;

        [ObservableProperty]
        public partial IBrush ColorLockForeground { get; set; } = Brushes.Gray;

        [ObservableProperty]
        public partial IBrush EffectsForeground { get; set; } = Brushes.LightGray;

        public void ToggleVisibility()
        {
            Layer.ToggleLayerVisibility();
            SyncFromModel();
        }

        public void ToggleColorLock(Action? selectLayerAction)
        {
            if (!IsSelected)
                selectLayerAction?.Invoke();

            Layer.SourceNode.LockTransparentPixels = !Layer.SourceNode.LockTransparentPixels;
            SyncFromModel();
        }

        public void ToggleEffects()
        {
            Layer.SourceNode.ShowEffects = !Layer.SourceNode.ShowEffects;
            _viewPortRefreshService.Refresh();
            SyncFromModel();
        }

        public void SyncFromModel()
        {
            var preview = Layer.Preview;
            PreviewBrush = preview != null ? new ImageBrush(preview.ToBitmap()) : Brushes.Transparent;
            IsSelected = Layer.IsSelected;
            ShowColorLockButton = Layer.IsSelected || Layer.SourceNode.LockTransparentPixels;
            HasEffects = Layer.SourceNode.HasEffects;
            ShowBlendModeName = Layer.SourceNode.BlendMode != SKBlendMode.SrcOver;
            BlendModeText = Layer.SourceNode.BlendMode.ToString();
            var hasUserName = !Pix2dSprite.IsGeneratedLayerName(Layer.SourceNode.Name);
            LayerName = hasUserName ? Layer.SourceNode.Name : string.Empty;
            ShowNameStrip = hasUserName;
            VisibilityForeground = Layer.SourceNode.IsVisible ? Brushes.White : Brushes.Gray;
            ColorLockForeground = Layer.SourceNode.LockTransparentPixels ? Brushes.White : Brushes.Gray;
            EffectsForeground = Layer.SourceNode.ShowEffects ? Brushes.White : Brushes.LightGray;
        }
    }
}