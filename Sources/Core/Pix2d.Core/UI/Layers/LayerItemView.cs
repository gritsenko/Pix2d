using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Common.Extensions;
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
                new Grid()
                    .Margin(0)
                    .Width(80)
                    .Height(80)
                    .Children(
                        new Button()
                            .Padding(0)
                            .OnClick(_ => LeftPointerPressed?.Invoke())
                            .Template(ButtonTemplate)
                            .Content(
                                new Rectangle()
                                    .Width(100)
                                    .Height(100)
                                    .Fill(state, x => x.PreviewBrush))
                            .OnPointerPressed(OnRightPointerPressed),
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
                    )
            );

    public Action? LeftPointerPressed { get; set; }

    public Action? RightPointerPressed { get; set; }

    private void OnRightPointerPressed(PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            RightPointerPressed?.Invoke();
        }
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
            VisibilityForeground = Layer.SourceNode.IsVisible ? Brushes.White : Brushes.Gray;
            ColorLockForeground = Layer.SourceNode.LockTransparentPixels ? Brushes.White : Brushes.Gray;
            EffectsForeground = Layer.SourceNode.ShowEffects ? Brushes.White : Brushes.LightGray;
        }
    }
}