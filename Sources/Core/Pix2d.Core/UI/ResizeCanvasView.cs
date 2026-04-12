using Pix2d.Messages;
using SkiaSharp;

namespace Pix2d.UI;

public class ResizeCanvasView : ComponentBase
{
    protected override StyleGroup? BuildStyles() => 
    [
        new Style<Button>()
            .CornerRadius(6)
            .FontSize(12)
    ];
    protected override object Build()
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
                                    .Value(() => CanvasWidth, v =>
                                    {
                                        CanvasWidth = (int)(v ?? 0);
                                        if (KeepAspect)
                                            CanvasHeight = (int)(CanvasWidth / _aspectRatio);
                                    }),

                                new TextBlock().Col(2).Text(L("Height")).FontSize(12).Foreground(Brushes.Gray),
                                new NumericUpDown().Col(2).Row(1)
                                    .FormatString("N0")
                                    .Value(() => CanvasHeight, v =>
                                    {
                                        CanvasHeight = (int)(v ?? 0);
                                        if (KeepAspect)
                                            CanvasWidth = (int)(CanvasHeight * _aspectRatio);
                                    })
                            ),

                        new ToggleSwitch()
                            .Content(L("Keep aspect ratio"))
                            .IsChecked(() => KeepAspect, v =>
                            {
                                KeepAspect = (bool)v!;

                                if (CanvasWidth != OriginalWidth)
                                    CanvasHeight = (int)(CanvasWidth / _aspectRatio);

                                if (_canvasHeight != OriginalHeight)
                                    CanvasWidth = (int)(CanvasHeight * _aspectRatio);
                            }),

                        new Separator().Height(1).Opacity(0.2),

                        new TextBlock().Text(L("Resize mode")).FontSize(12).Foreground(Brushes.Gray),
                        new ComboBox()
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Items(
                                new ComboBoxItem().Content(L("Canvas (Crop/Expand)")),
                                new ComboBoxItem().Content(L("Image (Rescale)"))
                            )
                            .SelectedIndex(() => ResizeMode, v => ResizeMode = (int)v!),

                        // Секция Якоря (Anchor)
                        new TextBlock().Text(L("Anchor")).FontSize(12).Foreground(Brushes.Gray)
                            .IsVisible(() => ResizeMode == 0),

                        new Border()
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .IsVisible(() => ResizeMode == 0)
                            .Child(CreateAnchorGrid()), // Метод создания сетки 3х3

                        new StackPanel()
                            .Margin(0, 10, 0, 0)
                            .Orientation(Orientation.Horizontal)
                            .HorizontalAlignment(HorizontalAlignment.Right)
                            .Spacing(8)
                            .Children(
                                new Button().Content(L("Reset")).OnClick(_ => OnResetCommandExecute()).Classes("btn")
                                    .Width(80)
                                    .Height(30)
                                    .IsEnabled(() => OriginalWidth != CanvasWidth || OriginalHeight != CanvasHeight),
                                new Button().Content(L("Apply")).Classes("accent") // Используйте акцентный цвет темы
                                    .Width(80)
                                    .Height(30)
                                    .Background(Brushes.CornflowerBlue)
                                    .OnClick(_ => OnResizeCanvasCommandExecute())
                            )
                    )
            );
    }

    private Control CreateAnchorGrid()
    {
        var grid = new Grid()
            .Width(72).Height(72)
            .Cols("24, 24, 24").Rows("24, 24, 24");

        for (int y = 0; y < 3; y++) // 0: Top/Left, 1: Center, 2: Bottom/Right
        {
            for (int x = 0; x < 3; x++)
            {
                int row = y;
                int col = x;

                var btn = new Button()
                    .Row(row).Col(col)
                    .Padding(0)
                    .Margin(1)
                    .HorizontalContentAlignment(HorizontalAlignment.Center)
                    .VerticalContentAlignment(VerticalAlignment.Center)
                    // Логика подсветки активного якоря
                    .Background(() => (VerticalAnchor == row && HorizontalAnchor == col)
                        ? Brushes.CornflowerBlue : Brushes.Transparent)
                    .BorderBrush(Brushes.Gray)
                    .BorderThickness(1)
                    .OnClick(_ =>
                    {
                        VerticalAnchor = row;
                        HorizontalAnchor = col;
                        StateHasChanged();
                    });

                // Добавим маленькую точку в центр для наглядности
                if (row == 1 && col == 1) btn.Content("•");

                grid.Children(btn);
            }
        }
        return grid;
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

    private int _canvasHeight = 0;
    private int _horizontalAnchor = 0;
    private int _verticalAnchor = 0;
    public string OriginalSizeStr { get; set; } = string.Empty;

    private bool HasActiveArtboard => SelectionService.GetActiveContainer() != null;
    private int OriginalWidth => HasActiveArtboard ? (int)SelectionService.GetActiveContainer().Size.Width : 0;
    private int OriginalHeight => HasActiveArtboard ? (int)SelectionService.GetActiveContainer().Size.Height : 0;

    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }

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

    public int ResizeMode { get; set; }

    public bool KeepAspect { get; set; }

    protected override void OnAfterInitialized()
    {
        UpdateSizeProperties();
        Messenger.Register<NodesSelectedMessage>(this, NodesSelected);

        VerticalAnchor = 1;
        HorizontalAnchor = 1;

        StateHasChanged();
    }

    private void NodesSelected(NodesSelectedMessage obj)
    {
        UpdateSizeProperties();
    }

    private void UpdateSizeProperties()
    {
        CanvasWidth = OriginalWidth;
        CanvasHeight = OriginalHeight;

        _aspectRatio = (double)OriginalWidth / OriginalHeight;

        OriginalSizeStr = $"{OriginalWidth}x{OriginalHeight}";

        StateHasChanged();
    }

    private void OnResizeCanvasCommandExecute()
    {
        AppState.UiState.ShowCanvasResizePanel = false;

        if (ResizeMode == 0)
        {
            EditService.CropCurrentSprite(new SKSize(CanvasWidth, CanvasHeight), HorizontalAnchor * 0.5f, VerticalAnchor * 0.5f);
        }
        else
        {
            EditService.ResizeCurrentSprite(new SKSize(CanvasWidth, CanvasHeight));
        }

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