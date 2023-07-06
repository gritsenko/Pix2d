using System;
using Pix2d.Messages.Edit;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Shared;
using SkiaNodes;
using SkiaSharp;
using System.Collections.Generic;

namespace Pix2d.Views;

public class ArtworkPreviewView : ComponentBase
{
    protected override object Build()
    {
        return new Grid()
            .Height(50)
            .Width(50)
            .Rows("*,32")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                //new ScrollViewer()
                //    .Background(StaticResources.Brushes.CheckerTilesBrush)
                //    .Content(
                //        new SKImageView()
                //            .HorizontalAlignment(HorizontalAlignment.Center)
                //            .VerticalAlignment(VerticalAlignment.Center)
                //            .Source(Preview, bindingSource: this)
                //    ),
                new Grid().Row(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new ComboBox()
                            .ItemsSource(AvailableScales)
                            .SelectedItem(Scale, BindingMode.TwoWay, bindingSource: this)
                            .ItemTemplate(_itemTemplate)
                    )
            );
    }

    private IDataTemplate _itemTemplate =
        new FuncDataTemplate<int>((itemVm, ns)
            => new TextBlock().Text(itemVm.ToString()));

    [Inject] AppState AppState { get; }
    [Inject] IMessenger Messenger { get; }

    private SpriteEditor _editor;
    private ViewPort _viewPort;
    private double _scale = 1;

    public SKBitmapObservable Preview { get; } = new SKBitmapObservable();

    public List<int> AvailableScales { get; set; } = new();

    public double Scale
    {
        get => _scale; 
        set
        {
            _scale = value;
            OnPropertyChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        Messenger.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<UiState>(x => x.ShowPreviewPanel, UpdatePreview));
        Messenger.Register<NodeEditorChangedMessage>(this, msg => InvalidateEditor());
        Messenger.Register<OperationInvokedMessage>(this, msg => UpdatePreview());

        for (var i = 1; i <= 10; i++) AvailableScales.Add(i);
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

        _editor = newEditor as SpriteEditor;
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
            var w = (int)(sprite.Size.Width * Scale * sf);
            var h = (int)(sprite.Size.Height * Scale * sf);
            var scale = (float)(sf * Scale);
            var frameIndex = _editor.CurrentFrameIndex;

            var curBitmap = Preview.Bitmap;

            if (curBitmap == null || h != curBitmap.Height || w != curBitmap.Width)
            {
                curBitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);

                _viewPort = new ViewPort(curBitmap.Width, curBitmap.Height);
                _viewPort.Settings.RenderAdorners = false;

                if (Math.Abs(scale - 1f) > 0.1)
                {
                    _viewPort.ShowArea(sprite.GetBoundingBox());

                }
            }

            _editor.CurrentSprite.RenderFramePreview(frameIndex, ref curBitmap, _viewPort, sprite.UseBackgroundColor);

            Preview.SetBitmap(curBitmap);

            Width = curBitmap.Width;// / sf;
            Height = curBitmap.Height;// / sf;
        }
    }

}