using System.Diagnostics.CodeAnalysis;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Plugins.Drawing.Brushes;
using Pix2d.Plugins.Drawing.Nodes;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Primitives.Drawing;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Services;

public class DrawingService : IDrawingService
{
    private DrawingOperationFactory? _operationFactory;
    private readonly AppState _appState;
    private readonly IOperationService _operationService;
    private readonly IToolService _toolService;
    private SpriteEditorState SpriteEditorState => _appState.SpriteEditorState;


    private IDrawingLayer? _drawingLayer;

    public event EventHandler MirrorModeChanged;

    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IMessenger _messenger;

    public List<IPixelBrush> Brushes { get; set; } =
    [
        new SquareSolidBrush(),
        new CircleSolidBrush(),
        //new PencilBrush(),
        new SprayBrush()
    ];

    public IDrawingLayer DrawingLayer
    {
        get => _drawingLayer;
        private set => SetNewDrawingLayer(value);
    }

    public IDrawingTarget CurrentDrawingTarget { get; set; }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DrawingService))]
    public DrawingService(
        ISnappingService snappingService,
        IViewPortRefreshService viewPortRefreshService,
        IMessenger messenger,
        AppState appState,
        IOperationService operationService,
        IToolService toolService)
    {
        _appState = appState;
        _operationService = operationService;
        _toolService = toolService;
        _viewPortRefreshService = viewPortRefreshService;
        _messenger = messenger;

        SetNewDrawingLayer(new DrawingLayerNode() { AspectSnapper = snappingService });

        messenger.Register<CurrentToolChangedMessage>(this, OnCurrentToolChanged);
        messenger.Register<ProjectCloseMessage>(this, OnProjectClose);
        messenger.Register<ProjectLoadedMessage>(this, m => UpdateFromDesignerState());
        messenger.Register<CanvasSizeChangedMessage>(this, msg => UpdateDrawingTarget());
        messenger.Register<OperationInvokedMessage>(this, msg => OnOperationInvoked(msg));
        SpriteEditorState.WatchFor(x => x.CurrentBrushSettings, OnBrushChanged);
        SpriteEditorState.WatchFor(x => x.CurrentColor, OnColorChanged);
        SpriteEditorState.WatchFor(x => x.IsPixelPerfectDrawingModeEnabled, OnPixelPerfectModeChanged);
    }

    private void OnOperationInvoked(OperationInvokedMessage msg)
    {
        if (msg.Operation is IUpdateDrawingTarget op)
            UpdateDrawingTarget();
    }

    private void OnProjectClose(ProjectCloseMessage _)
    {
        CancelCurrentOperation();
    }

    private void OnPixelPerfectModeChanged()
    {
        DrawingLayer.IsPixelPerfectMode = SpriteEditorState.IsPixelPerfectDrawingModeEnabled;
    }

    private void OnBrushChanged()
    {
        _drawingLayer.Brush = SpriteEditorState.CurrentBrushSettings.Brush;
        SpriteEditorState.CurrentBrushSettings.InitBrush();
        Refresh();
    }

    private void OnColorChanged()
    {
        _drawingLayer.DrawingColor = SpriteEditorState.CurrentColor;
        Refresh();
    }

    private void OnCurrentToolChanged(CurrentToolChangedMessage message)
    {
        SetDrawingMode(message.NewTool is IDrawingTool);
    }

    private void SetNewDrawingLayer(IDrawingLayer newDrawingLayer)
    {
        _operationFactory = new DrawingOperationFactory(newDrawingLayer, _operationService);

        if (_drawingLayer != null)
        {
            _drawingLayer.DrawingApplied -= DrawingLayer_DrawingApplied;
            _drawingLayer.DrawingStarted -= DrawingLayerOnDrawingStarted;
            _drawingLayer.SelectionStarted -= DrawingLayerOnDrawingStarted;
            _drawingLayer.LayerModified -= DrawingLayerOnModified;
            _drawingLayer.SelectionTransformed -= DrawingLayerSelectionTransformed;
        }

        _drawingLayer = newDrawingLayer;
        _drawingLayer.DrawingColor = SpriteEditorState.CurrentColor;

        if (_drawingLayer != null)
        {
            _drawingLayer.DrawingApplied += DrawingLayer_DrawingApplied;
            _drawingLayer.SelectionStarted += DrawingLayerOnDrawingStarted;
            _drawingLayer.DrawingStarted += DrawingLayerOnDrawingStarted;
            _drawingLayer.LayerModified += DrawingLayerOnModified;
            _drawingLayer.SelectionTransformed += DrawingLayerSelectionTransformed;
        }
    }

    private void DrawingLayerSelectionTransformed(object? sender, SelectionTransformedEventArgs e)
    {
        _operationService.PushOperations(e.Operation);
    }

    private void DrawingLayerOnModified(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void DrawingLayerOnDrawingStarted(object? sender, EventArgs e)
    {
        StartNewDrawingOperation();
    }

    private void StartNewDrawingOperation()
    {
        _operationFactory?.StartNewDrawingOperation(CurrentDrawingTarget);
    }

    private void DrawingLayer_DrawingApplied(object? sender, DrawingAppliedEventArgs e)
    {
        // OPERATION FINISHED ON DIFFERENT LAYER/SPRITE
        //if (_currentDrawingOperation == null || CurrentDrawingTarget != _currentDrawingOperation.GetDrawingTarget())
        //{
        //    _currentDrawingOperation = null;
        //    _currentDrawingOperations.Clear();
        //    return;
        //}

        if (e.SaveToUndo) //not cancelled
        {
            _operationFactory?.FinishCurrentDrawingOperation();
        }
        else
        {
            _messenger.Send(new SelectedLayerChangedMessage());
        }

        Refresh();

        OnDrawn();
    }

    public IPixelBrush GetBrush<TBrush>()
    {
        return Brushes.First(x => x is TBrush);
    }

    private IDrawingTarget GetDrawingTargetFromCurrentSprite()
    {
        var sprite = _appState.CurrentProject?.CurrentEditedNode as IDrawingTarget;
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
            dln.IsVisible = active; // && sprites.Any();
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

        SpriteEditorState.BrushPresets = bps;

        SpriteEditorState.CurrentBrushSettings = SpriteEditorState.BrushPresets[0];
        SpriteEditorState.CurrentColor = SKColor.Parse("d2691e");
    }

    public void ClearCurrentLayer()
    {
        DrawingLayer?.ClearTarget();
        Refresh();
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
        OnDrawingTargetChanged();
    }

    public void SplitCurrentOperation()
    {
        if (_operationFactory.IsOperationStarted)
            _operationFactory?.PushCurrentOperationAndStartNew(CurrentDrawingTarget);
    }

    public void SetCurrentColor(SKColor value)
    {
        if (SpriteEditorState.CurrentColor != value)
            SpriteEditorState.CurrentColor = value;
    }

    public void SetDrawingTarget(IDrawingTarget target)
    {
        CurrentDrawingTarget = target;
        _drawingLayer.DrawingColor = SpriteEditorState.CurrentColor;

        UpdateDrawingTarget();
    }

    public void UpdateFromDesignerState()
    {
        var tool = _appState.ToolsState.CurrentTool?.ToolInstance;
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
                SpriteEditorState.CurrentColor = col;

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
        if (_drawingLayer == null)
            return;
        
        var pasteOperation = new PasteOperation(bitmap, pos, this.CurrentDrawingTarget, _drawingLayer, this, _toolService);
        _operationService.InvokeAndPushOperations(pasteOperation);
    }

    public void ChangeBrushSize(float delta)
    {
        var bscale = SpriteEditorState.CurrentBrushSettings.Scale;
        bscale = Math.Min(Math.Max(1, bscale + delta), 512);

        var brush = SpriteEditorState.CurrentBrushSettings.Clone();
        brush.Scale = bscale;

        SpriteEditorState.CurrentBrushSettings = brush;
    }

    protected virtual void OnDrawn() => _messenger.Send(new DrawingServiceOnDrawnMessage());

    protected virtual void OnDrawingTargetChanged() => _messenger.Send(new DrawingTargetChangedMessage());

    protected virtual void OnMirrorModeChanged() => MirrorModeChanged?.Invoke(this, EventArgs.Empty);

    public IPixelSelectionEditor GetSelectionEditor() => (IPixelSelectionEditor)DrawingLayer;

    public void SelectAll() => _drawingLayer.SelectAll();

    public void CancelCurrentOperation() => _operationFactory?.CancelCurrentOperation();

    public void Refresh() => _viewPortRefreshService.Refresh();
}