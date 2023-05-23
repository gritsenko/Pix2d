using System.Windows.Input;
using Mvvm;
using Pix2d.Messages;
using Pix2d.Mvvm;
using SkiaSharp;

namespace Pix2d.ViewModels;

public class ResizeCanvasSizeViewModel : Pix2dViewModelBase
{
    public ISelectionService SelectionService { get; }
    public IDrawingService DrawingService { get; }
    public IEditService EditService { get; }
    public IViewPortService ViewPortService { get; }
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    private double _aspectRatio;

    private int _width;
    private int _height;
    private int _horizontalAnchor = 0;
    private int _verticalAnchor = 0;

    public string OriginalSizeStr
    {
        get => Get<string>();
        set => Set(value);
    }

    public bool HasActiveArtboard => SelectionService.GetActiveContainer() != null;
    public int OriginalWidth => HasActiveArtboard ? (int)SelectionService.GetActiveContainer().Size.Width : 0;
    public int OriginalHeight => HasActiveArtboard ? (int)SelectionService.GetActiveContainer().Size.Height : 0;

    public int Width
    {
        get => _width;
        set
        {
            if (_width == value)
                return;
                
            _width = value;

            OnPropertyChanged();

            if (KeepAspect)
            {
                _height = (int) (Width/_aspectRatio);
                OnPropertyChanged(nameof(Height));
            }
            ResetCommand.RaiseCanExecuteChanged();
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            if (_height == value)
                return;

            _height = value;

            OnPropertyChanged();

            if (KeepAspect)
            {
                _width = (int) (Height * _aspectRatio);
                OnPropertyChanged(nameof(Width));
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
        get => Get<bool>();
        set
        {
            Set(value);

            if (!value) return;

            if (_width != OriginalWidth)
            {
                _height = (int)(Width / _aspectRatio);
                OnPropertyChanged(nameof(Height));
            }

            if (_height != OriginalHeight)
            {
                _width = (int)(Height * _aspectRatio);
                OnPropertyChanged(nameof(Width));
            }

            ResetCommand.RaiseCanExecuteChanged();
        }
    }

    public IRelayCommand ResetCommand => GetCommand(OnResetCommandExecute, "Reset size", () => OriginalWidth != Width || OriginalHeight != Height);

    public ICommand ResizeCanvasCommand => GetCommand(OnResizeCanvasCommandExecute, "Resize canvas");

    public ResizeCanvasSizeViewModel(ISelectionService selectionService, IDrawingService drawingService, IEditService editService, IViewPortService viewPortService,
        IMessenger messenger, AppState appState)
    {
        SelectionService = selectionService;
        DrawingService = drawingService;
        EditService = editService;
        ViewPortService = viewPortService;
        Messenger = messenger;
        AppState = appState;

        if (IsDesignMode)
            return;

        Messenger.Register<NodesSelectedMessage>(this, NodesSelected);
        UpdateSizeProperties();
    }

    private void NodesSelected(NodesSelectedMessage obj)
    {
        UpdateSizeProperties();
    }

    public void UpdateSizeProperties()
    {
        Width = OriginalWidth;
        Height = OriginalHeight;

        _aspectRatio = (double) OriginalWidth/OriginalHeight;

        OriginalSizeStr = $"{OriginalWidth}x{OriginalHeight}";
    }

    private void OnResizeCanvasCommandExecute()
    {
        AppState.UiState.ShowCanvasResizePanel = false;
        EditService.CropCurrentSprite(new SKSize(Width, Height), HorizontalAnchor * 0.5f, VerticalAnchor * 0.5f);
        ViewPortService.ShowAll();
    }

    private void OnResetCommandExecute()
    {
        Width = OriginalWidth;
        Height = OriginalHeight;

        _aspectRatio = (double)OriginalWidth / OriginalHeight;

        OriginalSizeStr = $"{OriginalWidth}x{OriginalHeight}";
    }

}