using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.State;
using Pix2d.Views.Text;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Drawing.Tools
{
    public class PixelTextTool : BaseTool, IDrawingTool
    {

        public static ToolSettings ToolSettings { get; } = new()
        {
            DisplayName = "Pixels text tool",
            HotKey = null,
            TopBarUI = () => new TextBarView()
        };

        private TextNode _textNode = new TextNode();
        private SKPoint _selectionPosition;
        private string _text;
        private string _selectedFont;
        private float _fontSize = 14;
        private bool _isBold;
        private bool _isItalic;
        private bool _isAliased;
        private DrawingOperation _pixelSelectDrawingOperation;

        public string Text
        {
            get => _text;
            set
            {
                _text = value; 
                UpdateText();
            }
        }

        public string SelectedFont
        {
            get => _selectedFont;
            set
            {
                _selectedFont = value;
                UpdateText();
            }
        }

        public float FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                UpdateText();
            }
        }

        public bool IsBold
        {
            get => _isBold;
            set
            {
                _isBold = value;
                UpdateText();
            }
        }

        public bool IsItalic
        {
            get => _isItalic;
            set
            {
                _isItalic = value;
                UpdateText();
            }
        }

        public bool IsAliased
        {
            get => _isAliased;
            set
            {
                _isAliased = value;
                UpdateText();
            }
        }


        public IDrawingService DrawingService { get; }
        public IMessenger Messenger { get; }
        public AppState AppState { get; }
        public override string DisplayName => ToolSettings.DisplayName;

        private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

        public override EditContextType EditContextType => EditContextType.Sprite;

        public PixelTextTool(IDrawingService drawingService, IMessenger messenger, AppState appState)
        {
            DrawingService = drawingService;
            Messenger = messenger;
            AppState = appState;
        }

        public override async Task Activate()
        {
            _pixelSelectDrawingOperation = null;

            DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.MoveSelection);
            DrawingLayer.SelectionTransformed += DrawingLayerOnSelectionTransformed;
            DrawingLayer.PixelsBeforeSelected += DrawingLayerOnPixelsBeforeSelected;
            DrawingLayer.SelectionRemoved += DrawingLayer_SelectionRemoved;

            AppState.DrawingState.WatchFor(x => x.CurrentColor, OnStateColorPropertyChanged);

            AppState.UiState.ShowTextBar = true;
            
            await base.Activate();

            Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        }

        private void DrawingLayer_SelectionRemoved(object sender, EventArgs e)
        {
            _text = "";
        }


        public void ApplyText(string text)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                DrawingLayer.DeactivateSelectionEditor();
            }
            else
            {
                DrawingLayer.ApplySelection(true);
                _text = "";
            }
        }

        private void DrawingLayerOnSelectionTransformed(object sender, EventArgs e)
        {
            _selectionPosition = DrawingLayer.GetSelectionLayer().Position;
        }

        public override void Deactivate()
        {
            base.Deactivate();

            AppState.UiState.ShowTextBar = false;

            _text = "";

            DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
            DrawingLayer.SelectionRemoved -= DrawingLayer_SelectionRemoved;

            AppState.DrawingState.Unwatch(x => x.CurrentColor, OnStateColorPropertyChanged);

            Messenger.Unregister<OperationInvokedMessage>(this, OnOperationInvoked);
            DrawingLayer.ApplySelection(true);
        }

        private void OnStateColorPropertyChanged()
        {
            UpdateText();
        }

        public void UpdateText()
        {
            var textBm = BuildTextBitmap();
            if (textBm != null)
            {
                if (_selectionPosition == default)
                {
                    _selectionPosition = ((SKNode)DrawingLayer).GetLocalPosition(CoreServices.ViewPortService.ViewPort.ViewPortCenterGlobal);
                }
                DrawingLayer.SetSelectionFromExternal(textBm, _selectionPosition);
            }
            else
            {
                DrawingLayer.DeactivateSelectionEditor();
            }
            
            CoreServices.ViewPortService.Refresh();
        }

        private SKBitmap BuildTextBitmap()
        {
            var color = AppState.DrawingState.CurrentColor;

            if (string.IsNullOrWhiteSpace(Text))
            {
                return null;
            }
            
            _textNode.Text = Text;

            _textNode.FontSize = FontSize > 0 ? FontSize : 1;
            if (!string.IsNullOrWhiteSpace(SelectedFont))
                _textNode.FontFamily = SelectedFont;
            _textNode.Color = color;

            _textNode.Bold = IsBold;
            _textNode.Italic = IsItalic;
            _textNode.Aliased = IsAliased;

            _textNode.ResizeToText();
            if (_textNode.GetBoundingBox().IsEmpty)
                return null;

            var bitmap = new SKNode[]{ _textNode }.RenderToBitmap();
            return bitmap;
        }

        private void DrawingLayerOnPixelsBeforeSelected(object sender, EventArgs e)
        {
            if (_pixelSelectDrawingOperation != null && DrawingLayer.HasSelectionChanges)
            {
                //                _pixelSelectDrawingOperation.SetFinalData();
                //                _pixelSelectDrawingOperation.PushToHistory();
            }

            _pixelSelectDrawingOperation = new DrawingOperation(DrawingLayer.DrawingTarget);
        }

        private void OnOperationInvoked(OperationInvokedMessage e)
        {
            if (e.Operation is MoveOperation)
            {
                if (e.OperationType != OperationEventType.Perform)
                    DrawingLayer.InvalidateSelectionEditor();
            }
            else
            {
                // DrawingLayer.DeactivateSelectionEditor();
            }
        }

        public void ApplySelection()
        {
            DrawingLayer.ApplySelection();
        }
    }
}