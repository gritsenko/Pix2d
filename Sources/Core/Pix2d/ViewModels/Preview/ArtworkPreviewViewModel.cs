using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm.Messaging;
using Pix2d.Abstract.State;
using Pix2d.Abstract.UI;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Mvvm;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives.SpriteEditor;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.ViewModels.Preview
{
    public class ArtworkPreviewViewModel : Pix2dViewModelBase
    {
        public IMenuController MenuController { get; }
        public IAppState AppState { get; }
        public IMessenger Messenger { get; }

        private SpriteEditor _editor;
        private ViewPort _viewPort;

        public SKBitmapObservable Preview { get; } = new SKBitmapObservable();

        public double Width
        {
            get => Get<double>();
            set => Set(value);
        }
        public double Height
        {
            get => Get<double>();
            set => Set(value);
        }
        public List<ScaleItem> AvailableScales { get; set; } = new List<ScaleItem>();

        public ScaleItem SelectedScaleItem
        {
            get => Get<ScaleItem>();
            set
            {
                if(Set(value))
                {
                    UpdatePreview();
                }
            }
        }

        public double Scale => SelectedScaleItem.Scale;

        public ICommand SelectScaleCommand => GetCommand<ScaleItem>((item) =>
        {
            SelectedScaleItem = item;
        });

        public ArtworkPreviewViewModel(IMenuController menuController, IAppState appState, IMessenger messenger)
        {
            if (IsDesignMode)
                return;

            MenuController = menuController;
            AppState = appState;
            Messenger = messenger;

            if (MenuController is INotifyPropertyChanged vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(MenuController.ShowPreviewPanel))
                    {
                        UpdatePreview();
                    }
                };
            }

            Messenger.Register<NodeEditorChangedMessage>(this, OnEditorChanged);
            Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);

            for (int i = 1; i <= 10; i++)
            {
                AvailableScales.Add(new ScaleItem(i, SelectScaleCommand));
            }

            SelectedScaleItem = AvailableScales[0];
        }

        private void OnOperationInvoked(OperationInvokedMessage obj)
        {
            UpdatePreview();
        }

        private void OnEditorChanged(NodeEditorChangedMessage obj)
        {
            InvalidateEditor();
        }

        private void InvalidateEditor()
        {
            var newEditor = AppState.CurrentProject.CurrentNodeEditor;
            if (_editor == newEditor)
                return;

            if (_editor != null)
            {
                _editor.CurrentFrameChanged -= EditorOnCurrentFrameChanged;
                _editor.LayerChanged -= EditorOnLayerChanged;
            }

            _editor = newEditor as SpriteEditor;
            UpdatePreview();

            if (_editor != null)
            {
                _editor.CurrentFrameChanged += EditorOnCurrentFrameChanged;
                _editor.LayerChanged += EditorOnLayerChanged;
            }
        }

        private void EditorOnLayerChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void EditorOnCurrentFrameChanged(object sender, SpriteFrameChangedEvenArgs e)
        {
            UpdatePreview();
        }

        public void UpdatePreview()
        {
            if(!MenuController.ShowPreviewPanel)
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

        protected override void OnLoad()
        {
            InvalidateEditor();
        }
    }
}
