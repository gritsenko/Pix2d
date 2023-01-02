using System;
using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Primitives.Drawing;
using Pix2d.Tools;
using SkiaNodes;
using SkiaNodes.Interactive;

namespace Pix2d.Drawing.Tools
{
    public class ShapeTool : BaseTool, IDrawingTool
    {
        public IDrawingService DrawingService { get; }
        private ShapeBuilderBase _currentBuilder;
        private ShapeType _shapeType = ShapeType.Rectangle;

        public event EventHandler ShapeTypeChanged;

        public override EditContextType EditContextType => EditContextType.Sprite;

        public ShapeType ShapeType
        {
            get => _shapeType;
            set
            {
                if (_shapeType != value)
                {
                    _shapeType = value;
                    UpdateShapeBuilder(value);
                    ShapeTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void UpdateShapeBuilder(ShapeType value)
        {
            switch (value)
            {
                case ShapeType.Rectangle:
                    _currentBuilder = new RectangleShapeBuilder();
                    break;
                case ShapeType.Oval:
                    _currentBuilder = new OvalShapeBuilder();
                    break;
                case ShapeType.Line:
                    _currentBuilder = new LineShapeBuilder();
                    break;
                //case ShapeType.Circle:
                //    break;
                case ShapeType.Triangle:
                    _currentBuilder = new TriangleShapeBuilder();
                    break;
                //case ShapeType.Star:
                //    break;
                //case ShapeType.Arrow:
                //    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
            //_currentBuilder.Initialize(DrawingService.DrawingLayer, DrawingService.CurrentColor, Opacity, LineThickness);
        }

        public override string DisplayName => "Shape tool";

        public ShapeTool(IDrawingService drawingService)
        {
            DrawingService = drawingService;
        }

        public override Task Activate()
        {
            UpdateShapeBuilder(ShapeType);
            DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
            return base.Activate();
        }

        protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
        {
            _currentBuilder.Reset();
            _currentBuilder.BeginDrawing();
            _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode) DrawingService.DrawingLayer));
        }

        protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
        {
            if (_currentBuilder.AddPointInputMode == AddPointInputMode.PressAndHold && !e.Pointer.IsPressed)
            {
                //_currentBuilder.Cancel();
                return;
            }

            _currentBuilder.SetNextPointPreview(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
        }

        protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
        {
            if (_currentBuilder.AddPointInputMode == AddPointInputMode.PressAndHold)
            {
                _currentBuilder.AddPoint(e.Pointer.GetPosition((SKNode)DrawingService.DrawingLayer));
            }
        }
    }
}