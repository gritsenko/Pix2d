using System;
using System.Collections.Generic;
using System.Linq;
using CommonServiceLocator;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Brushes;
using Pix2d.Drawing.Nodes;
using Pix2d.Drawing.Tools;
using Pix2d.Messages;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Primitives.Drawing;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services
{
    public class DrawingService : IDrawingService
    {
        public ISelectionService SelectionService { get; }
        public IToolService ToolService { get; }
        public IViewPortService ViewPortService { get; }
        public AppState AppState { get; }
        public DrawingState DrawingState => AppState.DrawingState;

        private DrawingOperation _currentDrawingOperation;

        private IDrawingLayer _drawingLayer;

        public event EventHandler MirrorModeChanged;
        public event EventHandler Drawn;
        public event EventHandler DrawingTargetChanged;

        public List<IPixelBrush> Brushes { get; set; } = new()
        {
            new SquareSolidBrush(),
            new CircleSolidBrush(),
            //new PencilBrush(),
            new SprayBrush(),
        };

        public IDrawingLayer DrawingLayer
        {
            get => _drawingLayer;
            set => SetNewDrawingLayer(value);
        }

        public IDrawingTarget CurrentDrawingTarget { get; set; }

        // public List<BrushSettings> BrushPresets { get; set; } = new();

        public DrawingService(ISelectionService selectionService, IToolService toolService, ISnappingService snappingService, IViewPortService viewPortService,
            IMessenger messenger, AppState appState)
        {

            SelectionService = selectionService;
            ToolService = toolService;
            ViewPortService = viewPortService;
            AppState = appState;

            SetNewDrawingLayer(new DrawingLayerNode() { AspectSnapper = snappingService });

            messenger.Register<CurrentToolChangedMessage>(this, OnCurrentToolChanged);
            messenger.Register<ProjectLoadedMessage>(this, m => UpdateFromDesignerState());
            messenger.Register<CanvasSizeChanged>(this, msg => UpdateDrawingTarget());

            DrawingState.WatchFor(x => x.CurrentBrushSettings, OnBrushChanged);
            DrawingState.WatchFor(x => x.CurrentColor, OnColorChanged);
            DrawingState.WatchFor(x => x.IsPixelPerfectDrawingModeEnabled, OnPixelPerfectModeChanged);
        }

        private void OnPixelPerfectModeChanged()
        {
            DrawingLayer.IsPixelPerfectMode = DrawingState.IsPixelPerfectDrawingModeEnabled;
        }

        private void OnBrushChanged()
        {
            _drawingLayer.Brush = DrawingState.CurrentBrushSettings.Brush;
            DrawingState.CurrentBrushSettings.InitBrush();
            ViewPortService.Refresh();
        }
        private void OnColorChanged()
        {
            _drawingLayer.DrawingColor = DrawingState.CurrentColor;
            ViewPortService.Refresh();
        }

        private void OnCurrentToolChanged(CurrentToolChangedMessage message)
        {
            SetDrawingMode(message.NewTool is IDrawingTool);
        }

        private void SetNewDrawingLayer(IDrawingLayer newDrawingLayer)
        {
            if (_drawingLayer != null)
            {
                _drawingLayer.DrawingApplied -= DrawingLayer_DrawingApplied;
                _drawingLayer.DrawingStarted -= DrawingLayerOnDrawingStarted;
                _drawingLayer.SelectionStarted -= DrawingLayerOnDrawingStarted;
                _drawingLayer.LayerModified -= DrawingLayerOnModified;
            }

            _drawingLayer = newDrawingLayer;
            _drawingLayer.DrawingColor = DrawingState.CurrentColor;

            if (_drawingLayer != null)
            {
                _drawingLayer.DrawingApplied += DrawingLayer_DrawingApplied;
                _drawingLayer.SelectionStarted += DrawingLayerOnDrawingStarted;
                _drawingLayer.DrawingStarted += DrawingLayerOnDrawingStarted;
                _drawingLayer.LayerModified += DrawingLayerOnModified;
            }
        }

        private void DrawingLayerOnModified(object sender, EventArgs e)
        {
            Refresh();
        }

        private void DrawingLayerOnDrawingStarted(object sender, EventArgs e)
        {
            _currentDrawingOperation = new DrawingOperation(CurrentDrawingTarget);
        }

        private void DrawingLayer_DrawingApplied(object sender, DrawingAppliedEventArgs e)
        {
            if (_currentDrawingOperation == null || CurrentDrawingTarget != _currentDrawingOperation.GetDrawingTarget())
                return;

            if (e.SaveToUndo)
            {
                _currentDrawingOperation.SetFinalData();
                _currentDrawingOperation.PushToHistory();
            }
            
            ViewPortService.Refresh();
            
            OnDrawn();
        }

        public IPixelBrush GetBrush<TBrush>()
        {
            return Brushes.First(x => x is TBrush);
        }

        private IDrawingTarget GetDrawingTargetFromCurrentSprite()
        {
            var editService = ServiceLocator.Current.GetInstance<IEditService>();
            var sprite = editService?.CurrentEditedNode as IDrawingTarget;
            return sprite;
        }

        public void SetDrawingMode(bool active)
        {
            var drawingTarget = GetDrawingTargetFromCurrentSprite();
            if (drawingTarget != null)
            {
                SetDrawingTarget(drawingTarget);
            }

            if (DrawingLayer is DrawingLayerNode dln)
            {
                dln.IsVisible = active;// && sprites.Any();
            }
        }

        public void InitBrushSettings()
        {
            var bps = new List<BrushSettings>
            {
                new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 1, Opacity = 1f },
                new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 2, Opacity = 1f },
                new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 3, Opacity = 1f },
                new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 4, Opacity = 1f },
                new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 5, Opacity = 1f },
                new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 4, Opacity = 1f },
                new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 6, Opacity = 1f },
                new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 8, Opacity = 1f },
                new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 10, Opacity = 1f },
                new() { Brush = GetBrush<SprayBrush>(), Scale = 16, Opacity = 0.1f }
            };

            DrawingState.BrushPresets = bps;

            DrawingState.CurrentBrushSettings = DrawingState.BrushPresets[0];
            DrawingState.CurrentColor = SKColor.Parse("d2691e");
        }

        public void ClearCurrentLayer()
        {
            DrawingLayer?.ClearTarget();
            ViewPortService.Refresh();
        }

        public void UpdateDrawingTarget()
        {
            CurrentDrawingTarget = GetDrawingTargetFromCurrentSprite();

            if (CurrentDrawingTarget == null)
                return;

            _drawingLayer.SetTarget(CurrentDrawingTarget);
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer((SKNode)CurrentDrawingTarget);
            adornerLayer.Add((SKNode)_drawingLayer);

            ((SKNode)_drawingLayer).Position = new SKPoint();
        }

        public void SetCurrentColor(SKColor value)
        {
            if(AppState.DrawingState.CurrentColor != value)
                AppState.DrawingState.CurrentColor = value;
        }

        public void SetDrawingTarget(IDrawingTarget target)
        {
            CurrentDrawingTarget = target;
            _drawingLayer.DrawingColor = DrawingState.CurrentColor;

            UpdateDrawingTarget();
            OnDrawingTargetChanged();
        }

        public void UpdateFromDesignerState()
        {
            var tool = AppState.CurrentProject.CurrentTool;
            if (tool == null) return;
            tool.Deactivate();
            tool.Activate();
        }

        public SKColor PickColorByPoint(SKPoint worldPos)
        {
            if (CurrentDrawingTarget != null)
            {
                var localPos = ((SKNode)CurrentDrawingTarget).GetLocalPosition(worldPos).ToSkPointI();
                var col = CurrentDrawingTarget.PickColorByPoint(localPos.X, localPos.Y);

                if (!col.Equals(SKColor.Empty))
                    DrawingState.CurrentColor = col;

                return col;
            }

            return SKColor.Empty;
        }

        public void SetMirrorMode(MirrorMode mode, bool enable)
        {
            if (mode == MirrorMode.Horizontal || mode == MirrorMode.Both)
                _drawingLayer.MirrorX = enable;

            if (mode == MirrorMode.Vertical || mode == MirrorMode.Both)
                _drawingLayer.MirrorY = enable;

            OnMirrorModeChanged();
        }

        public void PasteBitmap(SKBitmap bitmap, SKPoint pos)
        {
            ToolService.ActivateTool(nameof(PixelSelectTool));
            DrawingLayer?.ApplySelection();
            DrawingLayer?.SetSelectionFromExternal(bitmap, pos);
            
            Refresh();
        }

        public void ChangeBrushSize(float delta)
        {
            var bscale = DrawingState.CurrentBrushSettings.Scale;
            bscale = Math.Min(Math.Max(1, bscale + delta), 512);

            DrawingState.CurrentBrushSettings.Scale = bscale;
            DrawingState.CurrentBrushSettings.InitBrush();
        }

        protected virtual void OnDrawn()
        {
            Drawn?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDrawingTargetChanged()
        {
            DrawingTargetChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMirrorModeChanged()
        {
            MirrorModeChanged?.Invoke(this, EventArgs.Empty);
        }

        public IPixelSelectionEditor GetSelectionEditor()
        {
            return DrawingLayer as IPixelSelectionEditor;
        }

        public void SelectAll()
        {
            _drawingLayer.SelectAll();
        }

        public void CancelCurrentOperation()
        {
            _drawingLayer.CancelCurrentOperation();
        }

        public void Refresh()
        {
            ViewPortService.Refresh();
        }
    }
}