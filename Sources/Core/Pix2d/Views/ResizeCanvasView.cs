using System.Globalization;
using Mvvm;
using Pix2d.Messages;
using SkiaSharp;

namespace Pix2d.Views;

public class ResizeCanvasView : ComponentBase
{
    protected override object Build() {

        ResizeCommand = new LoggedRelayCommand(OnResizeCanvasCommandExecute, () => true, "Resize");
        ResetCommand = new LoggedRelayCommand(OnResetCommandExecute,
            () => OriginalWidth != CanvasWidth || OriginalHeight != CanvasHeight, "Reset size");

        return new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Child(
                new StackPanel()
                    .Margin(8)
                    .Children(
                        new Grid()
                            .Cols("*,*,*")
                            .Rows("20,*")
                            .Children(
                                new TextBlock()
                                    .Text("Width"),
                                new NumericUpDown()
                                    .Row(1)
                                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                                    .Increment(1)
                                    .Value(Bind(CanvasWidth, BindingMode.TwoWay)),
                                new TextBlock().Col(1)
                                    .Row(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Text("✕"),
                                new TextBlock()
                                    .Col(2)
                                    .Text("Height"),
                                new NumericUpDown().Col(2)
                                    .Row(1)
                                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                                    .Increment(1)
                                    .Value(Bind(CanvasHeight, BindingMode.TwoWay))
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text("Horizontal anchor"),

                        // The initial values are not displayed right away due to https://github.com/AvaloniaUI/Avalonia/issues/4610
                        new ComboBox()
                            .Margin(0, 8, 0, 0)
                            .SelectedIndex(Bind(HorizontalAnchor))
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Items(
                                new ComboBoxItem().Content("Left"),
                                new ComboBoxItem().Content("Center"),
                                new ComboBoxItem().Content("Right")
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text("Vertical anchor"),

                        new ComboBox()
                            .Margin(0, 8, 0, 0)
                            .SelectedIndex(Bind(VerticalAnchor))
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Items(
                                new ComboBoxItem().Content("Top"),
                                new ComboBoxItem().Content("Center"),
                                new ComboBoxItem().Content("Bottom")
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text("Keep aspect ratio"),

                        new ToggleSwitch().Margin(0, 0, 0, 0)
                            .IsChecked(Bind(KeepAspect, BindingMode.TwoWay)),

                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new Button()
                                    .Content("Apply")
                                    .Background(Brushes.CornflowerBlue)
                                    .Width(80)
                                    .Height(30)
                                    .Command(ResizeCommand),
                                new Button()
                                    .Content("Reset")
                                    .Background(Brushes.Gray)
                                    .Width(80)
                                    .Height(30)
                                    .Command(ResetCommand)
                            )
                    )
            );
    }

    public void UpdateData()
    {
        UpdateSizeProperties();
    }

    [Inject] ISelectionService SelectionService { get; set; } = null!;
    [Inject] IDrawingService DrawingService { get; set; } = null!;
    [Inject] IEditService EditService { get; set; } = null!;
    [Inject] IViewPortService ViewPortService { get; set; } = null!;
    [Inject] IMessenger Messenger { get; set; } = null!;
    [Inject] AppState AppState { get; set; } = null!;
    
    private double _aspectRatio;

    private int _canvasWidth;
    private int _canvasHeight;
    private int _horizontalAnchor = 0;
    private int _verticalAnchor = 0;
    private bool _keepAspect;
    private string _originalSizeStr;

    public string OriginalSizeStr
    {
        get => _originalSizeStr;
        set
        {
            if (value == _originalSizeStr) return;
            _originalSizeStr = value;
            OnPropertyChanged();
        }
    }

    public bool HasActiveArtboard => SelectionService.GetActiveContainer() != null;
    public int OriginalWidth => HasActiveArtboard ? (int)SelectionService.GetActiveContainer().Size.Width : 0;
    public int OriginalHeight => HasActiveArtboard ? (int)SelectionService.GetActiveContainer().Size.Height : 0;

    public int CanvasWidth
    {
        get => _canvasWidth;
        set
        {
            if (_canvasWidth == value)
                return;

            _canvasWidth = value;

            OnPropertyChanged();

            if (KeepAspect)
            {
                _canvasHeight = (int)(CanvasWidth / _aspectRatio);
                OnPropertyChanged(nameof(CanvasHeight));
            }
            ResetCommand.RaiseCanExecuteChanged();
        }
    }

    public int CanvasHeight
    {
        get => _canvasHeight;
        set
        {
            if (_canvasHeight == value)
                return;

            _canvasHeight = value;

            OnPropertyChanged();

            if (KeepAspect)
            {
                _canvasWidth = (int)(CanvasHeight * _aspectRatio);
                OnPropertyChanged(nameof(CanvasWidth));
            }
            ResetCommand.RaiseCanExecuteChanged();
        }
    }

    public int HorizontalAnchor
    {
        get => _horizontalAnchor;
        set
        {
            _horizontalAnchor = value;
            OnPropertyChanged();
        }
    }

    public int VerticalAnchor
    {
        get => _verticalAnchor;
        set
        {
            _verticalAnchor = value;
            OnPropertyChanged();
        }
    }

    public bool KeepAspect
    {
        get => _keepAspect;
        set
        {
            _keepAspect = value;

            OnPropertyChanged();

            if (!value) return;

            if (_canvasWidth != OriginalWidth)
            {
                _canvasHeight = (int)(CanvasWidth / _aspectRatio);
                OnPropertyChanged(nameof(CanvasHeight));
            }

            if (_canvasHeight != OriginalHeight)
            {
                _canvasWidth = (int)(CanvasHeight * _aspectRatio);
                OnPropertyChanged(nameof(CanvasWidth));
            }

            ResetCommand.RaiseCanExecuteChanged();
        }
    }

    public IRelayCommand ResizeCommand { get; private set; }
    public IRelayCommand ResetCommand { get; private set; }

    protected override void OnAfterInitialized()
    {
        UpdateSizeProperties();
        Messenger.Register<NodesSelectedMessage>(this, NodesSelected);
    }

    private void NodesSelected(NodesSelectedMessage obj)
    {
        UpdateSizeProperties();
    }

    public void UpdateSizeProperties()
    {
        CanvasWidth = OriginalWidth;
        CanvasHeight = OriginalHeight;

        _aspectRatio = (double)OriginalWidth / OriginalHeight;

        OriginalSizeStr = $"{OriginalWidth}x{OriginalHeight}";
    }

    private void OnResizeCanvasCommandExecute()
    {
        AppState.UiState.ShowCanvasResizePanel = false;
        EditService.CropCurrentSprite(new SKSize(CanvasWidth, CanvasHeight), HorizontalAnchor * 0.5f, VerticalAnchor * 0.5f);
        ViewPortService.ShowAll();
    }

    private void OnResetCommandExecute()
    {
        CanvasWidth = OriginalWidth;
        CanvasHeight = OriginalHeight;

        _aspectRatio = (double)OriginalWidth / OriginalHeight;

        OriginalSizeStr = $"{OriginalWidth}x{OriginalHeight}";
    }

}