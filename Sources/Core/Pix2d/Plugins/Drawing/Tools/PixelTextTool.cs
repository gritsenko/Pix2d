using System;
using System.Threading.Tasks;
using CommonServiceLocator;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.State;
using Pix2d.Abstract.Tools;
using Pix2d.Abstract.UI;
using Pix2d.CommonNodes;
using Pix2d.Drawing.Nodes;
using Pix2d.Messages;
using Pix2d.Mvvm;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Plugins.Drawing.ViewModels;
using Pix2d.Primitives.Operations;
using Pix2d.State;
using Pix2d.Tools;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Drawing.Tools
{
    public class PixelTextTool : BaseTool, IDrawingTool
    {
        public IDrawingService DrawingService { get; }
        public IMessenger Messenger { get; }
        public IAppState AppState { get; }
        private DrawingOperation _pixelSelectDrawingOperation;
        private TextBarViewModel _textVm;
        public override string DisplayName => "Pixel text tool";

        private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

        public override EditContextType EditContextType => EditContextType.Sprite;

        private TextNode _textNode = new TextNode();
        private SKPoint _selectionPosition;

        public PixelTextTool(IDrawingService drawingService, IMessenger messenger, IAppState appState)
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
            DrawingLayer.DrawingApplied += DrawingLayer_DrawingApplied;

            AppState.DrawingState.WatchFor(x => x.CurrentColor, OnStateColorPropertyChanged);

            var mc = ServiceLocator.Current.GetInstance<IMenuController>();
            mc.ShowTextBar = true;

            _textVm = ServiceLocator.Current.GetInstance<IViewModelService>().GetViewModel<TextBarViewModel>(true);
            _textVm.PropertyChanged += TextVm_PropertyChanged;
            _textVm.TextAplied += TextVmOnTextAplied;

            await base.Activate();

            Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        }

        private void DrawingLayer_DrawingApplied(object sender, EventArgs e)
        {
            
        }

        private void TextVmOnTextAplied(object sender, EventArgs e)
        {
            DrawingLayer.ApplySelection();
            _textVm.Text = "";
        }

        private void DrawingLayerOnSelectionTransformed(object sender, EventArgs e)
        {
            _selectionPosition = DrawingLayer.GetSelectionLayer().Position;
        }

        public override void Deactivate()
        {
            base.Deactivate();

            var mc = ServiceLocator.Current.GetInstance<IMenuController>();
            mc.ShowTextBar = false;

            _textVm.Text = "";
            _textVm.PropertyChanged -= TextVm_PropertyChanged;
            _textVm.TextAplied -= TextVmOnTextAplied;
            _textVm = null;

            DrawingLayer.PixelsBeforeSelected -= DrawingLayerOnPixelsBeforeSelected;
            DrawingLayer.DrawingApplied -= DrawingLayer_DrawingApplied;

            AppState.DrawingState.Unwatch(x => x.CurrentColor, OnStateColorPropertyChanged);

            Messenger.Unregister<OperationInvokedMessage>(this, OnOperationInvoked);
            DrawingLayer.ApplySelection();
        }

        private void TextVm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateText();
        }


        private void OnStateColorPropertyChanged()
        {
            UpdateText();
        }

        private void UpdateText()
        {
            var textBm = BuildTextBitmap();
            DrawingLayer.ClearSelection();
            if (textBm != null)
            {
                if (_selectionPosition == default)
                {
                    _selectionPosition = ((SKNode)DrawingLayer).GetLocalPosition(CoreServices.ViewPortService.ViewPort.ViewPortCenterGlobal);
                }
                DrawingLayer.SetSelectionFromExternal(textBm, _selectionPosition);
            }
            CoreServices.ViewPortService.Refresh();
        }

        private SKBitmap BuildTextBitmap()
        {
            if (_textVm == null)
                return null;

            var vmText = _textVm.Text;
            var fontFamily = _textVm.SelectedFont?.Name ?? "";
            var fontSize = _textVm.FontSize;
            var color = AppState.DrawingState.CurrentColor;
            var bold = _textVm.IsBold;
            var italic = _textVm.IsItalic;
            var aliased = _textVm.IsAliased;

            if (string.IsNullOrWhiteSpace(vmText))
            {
                return null;
            }
            
            _textNode.Text = vmText;

            _textNode.FontSize = fontSize;
            if (!string.IsNullOrWhiteSpace(fontFamily))
                _textNode.FontFamily = fontFamily;
            _textNode.Color = color;

            _textNode.Bold = bold;
            _textNode.Italic = italic;
            _textNode.Aliased = aliased;

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

            _pixelSelectDrawingOperation = new DrawingOperation(((DrawingLayerNode)DrawingLayer).DrawingTarget);
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
                DrawingLayer.DeactivateSelectionEditor();
            }
        }

        public void ApplySelection()
        {
            DrawingLayer.ApplySelection();
        }
    }
}