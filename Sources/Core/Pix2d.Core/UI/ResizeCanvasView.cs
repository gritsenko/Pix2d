using Pix2d.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace Pix2d.UI;

public partial class ResizeCanvasView(ISelectionService selectionService, IEditService editService, IViewPortService viewPortService, IMessenger messenger, AppState appState)
    : ViewBase<ResizeCanvasView.State>(new State(selectionService, editService, viewPortService, messenger, appState))
{
    protected override StyleGroup? BuildStyles() => 
    [
        new Style<Button>()
            .CornerRadius(6)
            .FontSize(12)
    ];

    protected override object Build(State state)
    {
        return new Border()
            .Padding(16)
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        // Секция размеров
                        new Grid().Cols("*, 16, *").Rows("Auto, Auto")
                            .Children(
                                new TextBlock().Text(L("Width")).FontSize(12).Foreground(Brushes.Gray),
                                new NumericUpDown().Row(1)
                                    .FormatString("N0")
                                    .Value(state, x => x.CanvasWidth, BindingMode.TwoWay),

                                new TextBlock().Col(2).Text(L("Height")).FontSize(12).Foreground(Brushes.Gray),
                                new NumericUpDown().Col(2).Row(1)
                                    .FormatString("N0")
                                    .Value(state, x => x.CanvasHeight, BindingMode.TwoWay)
                            ),

                        new ToggleSwitch()
                            .Content(L("Keep aspect ratio"))
                            .IsChecked(state, x => x.KeepAspect, BindingMode.TwoWay),

                        new Separator().Height(1).Opacity(0.2),

                        new TextBlock().Text(L("Resize mode")).FontSize(12).Foreground(Brushes.Gray),
                        new ComboBox()
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Items(
                                new ComboBoxItem().Content(L("Canvas (Crop/Expand)")),
                                new ComboBoxItem().Content(L("Image (Rescale)"))
                            )
                            .SelectedIndex(state, x => x.ResizeMode, BindingMode.TwoWay),

                        // Секция Якоря (Anchor)
                        new TextBlock().Text(L("Anchor")).FontSize(12).Foreground(Brushes.Gray)
                            .IsVisible(state, x => x.IsAnchorVisible),

                        new Border()
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .IsVisible(state, x => x.IsAnchorVisible)
                            .Child(CreateAnchorGrid(state)),

                        new StackPanel()
                            .Margin(0, 10, 0, 0)
                            .Orientation(Orientation.Horizontal)
                            .HorizontalAlignment(HorizontalAlignment.Right)
                            .Spacing(8)
                            .Children(
                                new Button().Content(L("Reset")).OnClick(_ => state.Reset()).Classes("btn")
                                    .Width(80)
                                    .Height(30)
                                    .IsEnabled(state, x => x.CanReset),
                                new Button().Content(L("Apply")).Classes("accent")
                                    .Width(80)
                                    .Height(30)
                                    .Background(Brushes.CornflowerBlue)
                                    .OnClick(_ => state.ApplyResize())
                            )
                    )
            );
    }

    private Control CreateAnchorGrid(State state)
    {
        return new ItemsControl()
            .Width(72).Height(72)
            .ItemsSource(state.AnchorButtons)
            .ItemsPanel(new FuncTemplate<Panel?>(() => new UniformGrid
            {
                Rows = 3,
                Columns = 3
            }))
            .ItemTemplate((State.AnchorButtonState item) =>
                new Button()
                    .Padding(0)
                    .Margin(1)
                    .HorizontalContentAlignment(HorizontalAlignment.Center)
                    .VerticalContentAlignment(VerticalAlignment.Center)
                    .Content(item, x => x.DisplayText, BindingMode.OneWay)
                    .Background(item, x => x.BackgroundBrush, BindingMode.OneWay)
                    .BorderBrush(Brushes.Gray)
                    .BorderThickness(1)
                    .Command(item, x => x.SelectCommand, BindingMode.OneWay)
            );
    }

    public void UpdateData()
    {
        ViewModel?.UpdateSizeProperties();
    }

    public sealed partial class State : ObservableObject
    {
        private readonly ISelectionService _selectionService;
        private readonly IEditService _editService;
        private readonly IViewPortService _viewPortService;
        private readonly AppState _appState;
        private double _aspectRatio = 1d;
        private bool _isSyncing;
        private bool _isUpdatingAnchorSelection;
        private int _originalWidth;
        private int _originalHeight;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanReset))]
        public partial decimal? CanvasWidth { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanReset))]
        public partial decimal? CanvasHeight { get; set; }

        [ObservableProperty]
        public partial int HorizontalAnchor { get; set; } = 1;

        [ObservableProperty]
        public partial int VerticalAnchor { get; set; } = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnchorVisible))]
        public partial int ResizeMode { get; set; }

        [ObservableProperty]
        public partial bool KeepAspect { get; set; }

        [ObservableProperty]
        public partial string OriginalSizeStr { get; set; } = string.Empty;

        public AnchorButtonState[] AnchorButtons { get; }

        public bool CanReset => _originalWidth != CurrentWidth || _originalHeight != CurrentHeight;

        public bool IsAnchorVisible => ResizeMode == 0;

        private bool HasActiveArtboard => _selectionService.GetActiveContainer() is not null;
        private int CurrentWidth => (int)(CanvasWidth ?? 0);
        private int CurrentHeight => (int)(CanvasHeight ?? 0);

        public State(ISelectionService selectionService, IEditService editService, IViewPortService viewPortService,
            IMessenger messenger, AppState appState)
        {
            _selectionService = selectionService;
            _editService = editService;
            _viewPortService = viewPortService;
            _appState = appState;
            AnchorButtons = CreateAnchorButtons();

            UpdateSizeProperties();
            SyncAnchorButtons();
            messenger.Register<NodesSelectedMessage>(this, _ => UpdateSizeProperties());
        }

        partial void OnCanvasWidthChanged(decimal? value)
        {
            if (_isSyncing || !KeepAspect || !value.HasValue || _aspectRatio <= 0)
                return;

            SetCanvasHeightFromWidth(value.Value);
        }

        partial void OnCanvasHeightChanged(decimal? value)
        {
            if (_isSyncing || !KeepAspect || !value.HasValue || _aspectRatio <= 0)
                return;

            SetCanvasWidthFromHeight(value.Value);
        }

        partial void OnKeepAspectChanged(bool value)
        {
            if (_isSyncing || !value || _aspectRatio <= 0)
                return;

            if (CurrentWidth != _originalWidth)
            {
                SetCanvasHeightFromWidth(CanvasWidth ?? 0);
            }
            else if (CurrentHeight != _originalHeight)
            {
                SetCanvasWidthFromHeight(CanvasHeight ?? 0);
            }
        }

        partial void OnHorizontalAnchorChanged(int value)
        {
            if (!_isUpdatingAnchorSelection)
                SyncAnchorButtons();
        }

        partial void OnVerticalAnchorChanged(int value)
        {
            if (!_isUpdatingAnchorSelection)
                SyncAnchorButtons();
        }

        public void SetAnchor(int verticalAnchor, int horizontalAnchor)
        {
            if (VerticalAnchor == verticalAnchor && HorizontalAnchor == horizontalAnchor)
                return;

            _isUpdatingAnchorSelection = true;
            VerticalAnchor = verticalAnchor;
            HorizontalAnchor = horizontalAnchor;
            _isUpdatingAnchorSelection = false;

            SyncAnchorButtons();
        }

        public void UpdateSizeProperties()
        {
            var activeContainer = _selectionService.GetActiveContainer();
            _originalWidth = activeContainer != null ? (int)activeContainer.Size.Width : 0;
            _originalHeight = activeContainer != null ? (int)activeContainer.Size.Height : 0;

            _isSyncing = true;
            CanvasWidth = _originalWidth;
            CanvasHeight = _originalHeight;
            _isSyncing = false;

            _aspectRatio = _originalHeight == 0 ? 1d : (double)_originalWidth / _originalHeight;
            OriginalSizeStr = $"{_originalWidth}x{_originalHeight}";
            OnPropertyChanged(nameof(CanReset));
        }

        public void ApplyResize()
        {
            _appState.UiState.ShowCanvasResizePanel = false;

            if (ResizeMode == 0)
            {
                _editService.CropCurrentSprite(new SKSize(CurrentWidth, CurrentHeight), HorizontalAnchor * 0.5f,
                    VerticalAnchor * 0.5f);
            }
            else
            {
                _editService.ResizeCurrentSprite(new SKSize(CurrentWidth, CurrentHeight));
            }

            _viewPortService.ShowAll();
        }

        public void Reset()
        {
            _isSyncing = true;
            CanvasWidth = _originalWidth;
            CanvasHeight = _originalHeight;
            _isSyncing = false;

            _aspectRatio = _originalHeight == 0 ? 1d : (double)_originalWidth / _originalHeight;
            OriginalSizeStr = $"{_originalWidth}x{_originalHeight}";
            OnPropertyChanged(nameof(CanReset));
        }

        private void SetCanvasHeightFromWidth(decimal width)
        {
            _isSyncing = true;
            CanvasHeight = Math.Max(decimal.Zero, (decimal)Math.Round((double)width / _aspectRatio));
            _isSyncing = false;
        }

        private void SetCanvasWidthFromHeight(decimal height)
        {
            _isSyncing = true;
            CanvasWidth = Math.Max(decimal.Zero, (decimal)Math.Round((double)height * _aspectRatio));
            _isSyncing = false;
        }

        private AnchorButtonState[] CreateAnchorButtons()
        {
            return Enumerable.Range(0, 9)
                .Select(index =>
                {
                    var row = index / 3;
                    var col = index % 3;

                    return new AnchorButtonState(this, row, col, row == 1 && col == 1 ? "•" : string.Empty);
                })
                .ToArray();
        }

        private void SyncAnchorButtons()
        {
            foreach (var button in AnchorButtons)
            {
                button.IsSelected = button.Row == VerticalAnchor && button.Column == HorizontalAnchor;
            }
        }

        public sealed partial class AnchorButtonState(State owner, int row, int column, string displayText) : ObservableObject
        {
            public int Row { get; } = row;
            public int Column { get; } = column;
            public string DisplayText { get; } = displayText;

            [ObservableProperty]
            [NotifyPropertyChangedFor(nameof(BackgroundBrush))]
            public partial bool IsSelected { get; set; }

            public IBrush BackgroundBrush => IsSelected ? Brushes.CornflowerBlue : Brushes.Transparent;

            [RelayCommand]
            private void Select()
            {
                owner.SetAnchor(Row, Column);
            }
        }
    }
}