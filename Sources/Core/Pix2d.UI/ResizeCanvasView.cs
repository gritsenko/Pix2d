using System.Globalization;
using Mvvm;
using Pix2d.Messages;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI;

public class ResizeCanvasView : LocalizedComponentBase
{
    protected override object Build()
    {
        return new Border()
            .Child(
                new StackPanel()
                    .Margin(8)
                    .Children(
                        new Grid()
                            .Cols("*,*,*")
                            .Rows("20,*")
                            .Children(
                                new TextBlock()
                                    .Text(L("Width")),
                                new NumericUpDown()
                                    .Row(1)
                                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                                    .Increment(1)
                                    .Value(CanvasWidth, BindingMode.TwoWay),
                                new TextBlock().Col(1)
                                    .Row(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Text("✕"),
                                new TextBlock()
                                    .Col(2)
                                    .Text(L("Height")),
                                new NumericUpDown().Col(2)
                                    .Row(1)
                                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                                    .Increment(1)
                                    .Value(CanvasHeight, BindingMode.TwoWay)
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text(L("Horizontal anchor")),

                        // The initial values are not displayed right away due to https://github.com/AvaloniaUI/CrossPlatformDesktop/issues/4610
                        new ComboBox()
                            .Margin(0, 8, 0, 0)
                            .SelectedIndex(HorizontalAnchor, BindingMode.TwoWay)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Items(
                                new ComboBoxItem().Content(L("Left")),
                                new ComboBoxItem().Content(L("Center")),
                                new ComboBoxItem().Content(L("Right"))
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text(L("Vertical anchor")),

                        new ComboBox()
                            .Margin(0, 8, 0, 0)
                            .SelectedIndex(VerticalAnchor, BindingMode.TwoWay)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Items(
                                new ComboBoxItem().Content(L("Top")),
                                new ComboBoxItem().Content(L("Center")),
                                new ComboBoxItem().Content(L("Bottom"))
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text(L("Keep aspect ratio")),

                        new ToggleSwitch().Margin(0, 0, 0, 0)
                            .IsChecked(Bind(KeepAspect, BindingMode.TwoWay)),

                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new Button()
                                    .Classes("btn")
                                    .Content(L("Apply"))
                                    .Background(Brushes.CornflowerBlue)
                                    .Width(80)
                                    .Height(30)
                                    .OnClick(_ => OnResizeCanvasCommandExecute()),
                                new Button()
                                    .Classes("btn")
                                    .Content(L("Reset"))
                                    .Background(Brushes.Gray)
                                    .Width(80)
                                    .Height(30)
                                    .IsEnabled(() => OriginalWidth != CanvasWidth || OriginalHeight != CanvasHeight)
                                    .OnClick(_ => OnResetCommandExecute())
                            )
                    )
            );
    }

    public void UpdateData()
    {
        UpdateSizeProperties();
    }

    [Inject] ISelectionService SelectionService { get; set; } = null!;
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
            StateHasChanged();
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
            StateHasChanged();
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

            StateHasChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        UpdateSizeProperties();
        Messenger.Register<NodesSelectedMessage>(this, NodesSelected);

        VerticalAnchor = 1;
        HorizontalAnchor = 1;
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