using System.Collections.ObjectModel;
using Pix2d.Abstract.Edit;
using Pix2d.Common;
using Pix2d.Messages;
using Pix2d.UI.Shared;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.UI;

public class ArtworkPreviewView : ComponentBase
{
    protected override object Build()
    {
        return new Grid()
            .Rows("Auto,Auto")
            .Children(
                new ScrollViewer()
                    .MaxWidth(300)
                    .MaxHeight(300)
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
                    .Content(
                        new SKImageView()
                            .ShowCheckerBackground(true)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Source(Preview, bindingSource: this)
                    ),
                new Grid().Row(1)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new ComboBox()
                            .Margin(6)
                            .ItemsSource(AvailableScales)
                            .SelectedItem(() => SelectedScale, v => SelectedScale = (ScaleItem)v)
                            .ItemTemplate(_itemTemplate)
                    )
            );
    }

    private readonly IDataTemplate _itemTemplate =
        new FuncDataTemplate<ScaleItem>((itemVm, ns)
            => new TextBlock().Text($"{itemVm?.Scale:F2}x"));

    [Inject] AppState AppState { get; }
    [Inject] IMessenger Messenger { get; }

    private ISpriteEditor _editor;
    private ViewPort _viewPort;
    
    private ScaleItem _selectedScale = new(1);

    public SKBitmapObservable Preview { get; } = new();

    public record ScaleItem(double Scale);

    public ObservableCollection<ScaleItem> AvailableScales { get; set; } = new();

    public ScaleItem SelectedScale
    {
        get => _selectedScale ?? new ScaleItem(1); 
        set
        {
            _selectedScale = value;
            OnPropertyChanged();
            UpdatePreview();
        }
    }

    protected override void OnAfterInitialized()
    {
        Messenger.Register<OperationInvokedMessage>(this, msg => UpdatePreview());

        AppState.UiState.WatchFor(x=>x.ShowPreviewPanel, UpdatePreview);
        AppState.CurrentProject.WatchFor(x => x.CurrentNodeEditor, InvalidateEditor);

        AvailableScales.Clear();
        for (var i = 5; i >= 2; i--) AvailableScales.Add(new ScaleItem(1f / i));
        for (var i = 1; i <= 10; i++) AvailableScales.Add(new ScaleItem(i));

        SelectedScale = AvailableScales.FirstOrDefault(x => Math.Abs(x.Scale - 1) < 0.01) ?? AvailableScales[0];
        UpdatePreview();
    }

    private void InvalidateEditor()
    {
        var newEditor = AppState.CurrentProject.CurrentNodeEditor;
        if (_editor == newEditor)
            return;

        //if (_editor != null)
        //{
        //    _editor.CurrentFrameChanged -= EditorOnCurrentFrameChanged;
        //    _editor.LayerChanged -= EditorOnLayerChanged;
        //}

        _editor = newEditor as ISpriteEditor;
        UpdatePreview();

        //if (_editor != null)
        //{
        //    _editor.CurrentFrameChanged += EditorOnCurrentFrameChanged;
        //    _editor.LayerChanged += EditorOnLayerChanged;
        //}
    }

    public void UpdatePreview()
    {
        if (!AppState.UiState.ShowPreviewPanel)
            return;

        if (_editor != null)
        {
            var sf = 1f;
            var sprite = _editor.CurrentSprite;
            var scale = (float)(sf * SelectedScale.Scale);
            var w = (int)(sprite.Size.Width * scale);
            var h = (int)(sprite.Size.Height * scale);
            var frameIndex = _editor.CurrentFrameIndex;

            var curBitmap = Preview.Bitmap;

            if (curBitmap == null || h != curBitmap.Height || w != curBitmap.Width)
            {
                curBitmap = new SKBitmap(w, h, Pix2DAppSettings.ColorType, SKAlphaType.Premul);

                _viewPort = new ViewPort(curBitmap.Width, curBitmap.Height);
                _viewPort.Settings.RenderAdorners = false;

                if (Math.Abs(SelectedScale.Scale - 1f) > 0.1)
                {
                    _viewPort.ShowArea(sprite.GetBoundingBox());
                }
            }

            _editor.CurrentSprite.RenderFramePreview(frameIndex, ref curBitmap, _viewPort, sprite.UseBackgroundColor);

            Preview.SetBitmap(curBitmap);
        }
    }

}