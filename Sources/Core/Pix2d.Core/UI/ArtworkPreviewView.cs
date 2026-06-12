using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Edit;
using Pix2d.Messages;
using Pix2d.UI.Shared;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.UI;

public partial class ArtworkPreviewView(AppState appState, IMessenger messenger)
    : ViewBase<ArtworkPreviewView.State>(new State(appState, messenger))
{
    protected override object Build(State state) =>
        new Grid()
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
                            .Source(state, x => x.Preview)
                    ),
                new Grid().Row(1)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new ComboBox()
                            .Margin(6)
                            .ItemsSource(state.AvailableScales)
                            .SelectedItem(state, x => x.SelectedScale, BindingMode.TwoWay)
                            .ItemTemplate(_itemTemplate)
                    )
            );

    private readonly IDataTemplate _itemTemplate =
        new FuncDataTemplate<ScaleItem>((itemVm, ns)
            => new TextBlock().Text($"{itemVm?.Scale:F2}x"));

    public sealed record ScaleItem(double Scale);

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private ISpriteEditor? _editor;
        private ViewPort? _viewPort;

        public SKBitmapObservable Preview { get; } = new();

        public ObservableCollection<ScaleItem> AvailableScales { get; } = [];

        [ObservableProperty]
        public partial ScaleItem? SelectedScale { get; set; } = new(1);

        partial void OnSelectedScaleChanged(ScaleItem? value)
        {
            UpdatePreview();
        }

        public State(AppState appState, IMessenger messenger)
        {
            _appState = appState;

            messenger.Register<OperationInvokedMessage>(this, _ => UpdatePreview());

            _appState.UiState.WatchFor(x => x.ShowPreviewPanel, UpdatePreview);
            _appState.WatchForCurrentProject(x => x.CurrentNodeEditor, InvalidateEditor);

            AvailableScales.Clear();
            for (var i = 5; i >= 2; i--) AvailableScales.Add(new ScaleItem(1f / i));
            for (var i = 1; i <= 10; i++) AvailableScales.Add(new ScaleItem(i));

            SelectedScale = AvailableScales.FirstOrDefault(x => Math.Abs(x.Scale - 1) < 0.01) ?? AvailableScales[0];
            InvalidateEditor();
            UpdatePreview();
        }

        private void InvalidateEditor()
        {
            var newEditor = _appState.CurrentProject.CurrentNodeEditor;
            if (_editor == newEditor)
                return;

            _editor = newEditor as ISpriteEditor;
            UpdatePreview();
        }

        public void UpdatePreview()
        {
            if (!_appState.UiState.ShowPreviewPanel)
                return;

            if (_editor == null)
                return;

            var sf = 1f;
            var sprite = _editor.CurrentSprite;
            var scale = (float)(sf * (SelectedScale?.Scale ?? 1d));
            var w = (int)(sprite.Size.Width * scale);
            var h = (int)(sprite.Size.Height * scale);
            var frameIndex = _editor.CurrentFrameIndex;

            var curBitmap = Preview.Bitmap;

            if (curBitmap == null || h != curBitmap.Height || w != curBitmap.Width)
            {
                curBitmap = new SKBitmap(w, h, Pix2DAppSettings.ColorType, SKAlphaType.Premul);

                _viewPort = new ViewPort(curBitmap.Width, curBitmap.Height);
                _viewPort.Settings.RenderAdorners = false;

                if (Math.Abs((SelectedScale?.Scale ?? 1d) - 1f) > 0.1)
                {
                    _viewPort.ShowArea(sprite.GetBoundingBox());
                }
            }

            _editor.CurrentSprite!.RenderFramePreview(frameIndex, ref curBitmap, _viewPort!, sprite.UseBackgroundColor);

            Preview.SetBitmap(curBitmap);
        }
    }
}