using System.Diagnostics.CodeAnalysis;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Plugins.Drawing.Brushes;
using Pix2d.Plugins.Drawing.Nodes;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
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

    public event EventHandler? MirrorModeChanged;

    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IMessenger _messenger;
    private readonly ISettingsService _settingsService;

    public List<IPixelBrush> Brushes { get; set; } =
    [
        new SquareSolidBrush(),
        new CircleSolidBrush(),
        //new PencilBrush(),
        new SprayBrush(),
        new MarkerBrush()
    ];

    public IDrawingLayer DrawingLayer
    {
        get => _drawingLayer!;
        private set => SetNewDrawingLayer(value);
    }

    public IDrawingTarget? CurrentDrawingTarget { get; set; }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DrawingService))]
    public DrawingService(
        ISnappingService snappingService,
        IViewPortRefreshService viewPortRefreshService,
        IMessenger messenger,
        AppState appState,
        IOperationService operationService,
        IToolService toolService,
        ISettingsService settingsService)
    {
        _appState = appState;
        _operationService = operationService;
        _toolService = toolService;
        _viewPortRefreshService = viewPortRefreshService;
        _messenger = messenger;
        _settingsService = settingsService;

        SetNewDrawingLayer(new DrawingLayerNode()
        {
            AspectSnapper = snappingService,
            ActiveToolKeyProvider = () => _appState.ToolsState.CurrentToolKey,
            ArtboardActivationResolver = TryActivateArtboardUnderPointer,
        });

        messenger.Register<ProjectCloseMessage>(this, OnProjectClose);
        messenger.Register<ProjectLoadedMessage>(this, m => UpdateFromDesignerState());
        messenger.Register<CanvasSizeChangedMessage>(this, msg => UpdateDrawingTarget());
        messenger.Register<OperationInvokedMessage>(this, msg => OnOperationInvoked(msg));
        _appState.ToolsState.WatchFor(x => x.CurrentToolKey, OnCurrentToolChanged);
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
        _drawingLayer!.Brush = SpriteEditorState.CurrentBrushSettings.Brush!;
        SpriteEditorState.CurrentBrushSettings.InitBrush();
        Refresh();
    }

    private void OnColorChanged()
    {
        _drawingLayer!.DrawingColor = SpriteEditorState.CurrentColor;
        Refresh();
    }

    private void OnCurrentToolChanged()
    {
        var currentTool = _appState.ToolsState.CurrentTool?.ToolInstance;
        SetDrawingMode(currentTool is IDrawingTool);
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
            _drawingLayer.MarqueeFinishedByUser -= DrawingLayer_MarqueeFinishedByUser;
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
            _drawingLayer.MarqueeFinishedByUser += DrawingLayer_MarqueeFinishedByUser;
        }
    }

    private void DrawingLayerSelectionTransformed(object? sender, SelectionTransformedEventArgs e)
    {
        _operationService.PushOperations(e.Operation);
    }

    private void DrawingLayer_MarqueeFinishedByUser(object? sender, EventArgs e)
    {
        // The event fires only from FinishSelection after the marquee is fully set up, so a downcast +
        // GetSelectionLayer/GetSelectionBackground is safe here. The op holds the same selection-layer
        // reference DrawingLayerNode uses, so subsequent TransformSelectionOperation/ApplyTransformOperation pushed
        // on top will share its state and chain consistently through undo/redo.
        if (_drawingLayer is not DrawingLayerNode dln) return;

        var selectionLayer = (SpriteSelectionNode)dln.GetSelectionLayer();
        var backgroundBitmap = dln.GetSelectionBackground();
        var toolKey = _appState.ToolsState.CurrentToolKey;

        var op = new BeginSelectionOperation(dln, selectionLayer, backgroundBitmap, toolKey);
        _operationService.PushOperations(op);

        if (!_appState.IsAutoOpenTransformEditorAfterSelectionEnabled)
            return;

        if (!_toolService.IsSelectionTool(toolKey) || toolKey == nameof(PixelTransformTool))
            return;

        _toolService.ActivateTool<PixelTransformTool>();
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
        if (CurrentDrawingTarget != null)
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

    private IDrawingTarget? GetDrawingTargetFromCurrentSprite()
    {
        var sprite = _appState.CurrentProject?.CurrentEditedNode as IDrawingTarget;
        return sprite;
    }

    /// <summary>
    /// Click-to-activate gate for multi-artboard scenes (wired into <see cref="DrawingLayerNode.ArtboardActivationResolver"/>).
    /// When a pointer press lands on an artboard other than the one currently edited, switch the active
    /// sprite to it and report the press as consumed so no stroke starts on the outgoing target. A press
    /// on the active artboard — or on empty space, so off-canvas strokes still work — draws normally.
    /// Single-artboard scenes never switch.
    /// </summary>
    private bool TryActivateArtboardUnderPointer(SKPoint worldPos)
    {
        var project = _appState.CurrentProject;
        var scene = project?.SceneNode;
        if (scene == null)
            return false;

        var sprites = scene.Nodes.OfType<Pix2dSprite>().ToArray();
        if (sprites.Length <= 1)
            return false;

        var spriteUnderPointer = sprites.FirstOrDefault(s => s.GetBoundingBox().Contains(worldPos));
        if (spriteUnderPointer == null || ReferenceEquals(spriteUnderPointer, project!.CurrentEditedNode))
            return false;

        _messenger.Send(new ActivateArtboardRequestedMessage(spriteUnderPointer));
        return true;
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
        var bps = BuildBuiltInPresets();
        bps.AddRange(LoadUserPresets());

        SpriteEditorState.BrushPresets = bps;

        SpriteEditorState.CurrentBrushSettings = SpriteEditorState.BrushPresets[0];
        SpriteEditorState.CurrentColor = SKColor.Parse("d2691e");
    }

    /// <summary>
    /// The shipped preset row. Deliberately not persisted: keeping built-ins in code means a later release can
    /// change them, and it makes "delete a built-in" unrepresentable rather than merely blocked.
    /// </summary>
    private List<BrushSettings> BuildBuiltInPresets() =>
    [
        new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 1, Opacity = 1f },
        new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 2, Opacity = 1f },
        new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 3, Opacity = 1f },
        new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 4, Opacity = 1f },
        new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 5, Opacity = 1f },
        new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 4, Opacity = 1f },
        new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 6, Opacity = 1f },
        new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 8, Opacity = 1f },
        new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 10, Opacity = 1f },
        new() { Brush = GetBrush<SprayBrush>(), Scale = 16, Opacity = 0.1f },
        new() { Brush = GetBrush<MarkerBrush>(), Scale = 16, Opacity = 0.5f }
    ];

    #region user brush presets

    // = nameof(AppSettings.UserBrushPresets). SettingsService resolves keys to AppSettings properties by
    // reflection and an undeclared key is a silently-dropped write, so this string and that property must
    // stay in lockstep (the trap that cost a release in the rate-prompt funnel).
    private const string UserBrushPresetsSettingKey = "UserBrushPresets";

    /// <summary>
    /// Reads the stored user presets, skipping anything that no longer resolves. Tolerant by design: a preset
    /// saved by a newer build (unknown brush key) or a hand-edited settings file must not stop the editor from
    /// starting — the same graceful-skip posture as the palette parser and the node-type binder.
    /// </summary>
    private List<BrushSettings> LoadUserPresets()
    {
        var result = new List<BrushSettings>();

        if (!_settingsService.TryGet<List<BrushPresetData>>(UserBrushPresetsSettingKey, out var stored)
            || stored == null)
            return result;

        foreach (var data in stored)
        {
            var brush = BrushKeys.Resolve(data.Brush, Brushes);
            if (brush == null)
            {
                Logger.Trace($"Skipping a saved brush preset with an unknown brush key '{data.Brush}'.");
                continue;
            }

            result.Add(new BrushSettings
            {
                Brush = brush,
                // Clamp to the same bounds the UI enforces (ChangeBrushSize / the opacity slider), so a
                // corrupted value can't produce an unusable or absurdly expensive brush.
                Scale = Math.Clamp(data.Scale, 1f, 512f),
                Opacity = Math.Clamp(data.Opacity, 0f, 1f),
                Spacing = Math.Clamp(data.Spacing, 0.01f, 10f),
                PressureAffectsSize = data.PressureAffectsSize,
                PressureAffectsOpacity = data.PressureAffectsOpacity,
                IsUserPreset = true
            });
        }

        return result;
    }

    public BrushSettings? SaveCurrentBrushAsPreset()
    {
        var current = SpriteEditorState.CurrentBrushSettings;
        if (current?.Brush == null || BrushKeys.GetKey(current.Brush) == null)
            return null;

        var presets = SpriteEditorState.BrushPresets;

        // Saving is idempotent: an identical preset (built-in or user) is returned instead of duplicated.
        // BrushSettings.Equals is value equality over everything that affects the stroke.
        var existing = presets.FirstOrDefault(p => p.Equals(current));
        if (existing != null)
            return existing;

        var preset = current.Clone();
        preset.IsUserPreset = true;

        // Assign a NEW list: StateBase watchers fire on property assignment, not on in-place mutation, so
        // adding to the existing instance would leave the presets row stale.
        SpriteEditorState.BrushPresets = [.. presets, preset];
        PersistUserPresets();

        return preset;
    }

    public bool DeleteBrushPreset(BrushSettings preset)
    {
        if (preset is not { IsUserPreset: true })
            return false;

        var presets = SpriteEditorState.BrushPresets;

        // By reference, NOT by value: BrushSettings has value equality, so List.Remove would happily delete a
        // built-in that happens to have identical settings.
        var index = presets.FindIndex(p => ReferenceEquals(p, preset));
        if (index < 0)
            return false;

        var updated = new List<BrushSettings>(presets);
        updated.RemoveAt(index);
        SpriteEditorState.BrushPresets = updated;
        PersistUserPresets();

        // CurrentBrushSettings is a clone, so the user keeps drawing with the same brush; only the row's
        // highlight needs clearing when the deleted tile was the selected one.
        if (ReferenceEquals(SpriteEditorState.CurrentPixelBrushPreset, preset))
            SpriteEditorState.CurrentPixelBrushPreset = null!;

        return true;
    }

    private void PersistUserPresets()
    {
        var data = SpriteEditorState.BrushPresets
            .Where(p => p.IsUserPreset)
            .Select(p => new BrushPresetData
            {
                Brush = BrushKeys.GetKey(p.Brush) ?? "",
                Scale = p.Scale,
                Opacity = p.Opacity,
                Spacing = p.Spacing,
                PressureAffectsSize = p.PressureAffectsSize,
                PressureAffectsOpacity = p.PressureAffectsOpacity
            })
            .Where(d => !string.IsNullOrEmpty(d.Brush))
            .ToList();

        _settingsService.Set(UserBrushPresetsSettingKey, data);
    }

    #endregion

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

        if (_drawingLayer == null)
            return;

        _drawingLayer.SetTarget(CurrentDrawingTarget);
        var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer((SKNode)CurrentDrawingTarget);
        adornerLayer.Add((SKNode)_drawingLayer);

        ((SKNode)_drawingLayer).Position = new SKPoint();
        OnDrawingTargetChanged();
    }

    public void SplitCurrentOperation()
    {
        if (_operationFactory?.IsOperationStarted == true && CurrentDrawingTarget != null)
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
        if (_drawingLayer != null)
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
        if (_drawingLayer == null)
            return;

        if (mode == MirrorMode.Horizontal || mode == MirrorMode.Both)
            _drawingLayer.MirrorX = enable;

        if (mode == MirrorMode.Vertical || mode == MirrorMode.Both)
            _drawingLayer.MirrorY = enable;

        OnMirrorModeChanged();
    }

    public void PasteBitmap(SKBitmap bitmap, SKPoint pos)
    {
        if (_drawingLayer == null || CurrentDrawingTarget == null)
            return;
        
        var pasteOperation = new PasteOperation(bitmap, pos, CurrentDrawingTarget, _drawingLayer, this, _toolService, _appState.ToolsState.CurrentToolKey);
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

    public IPixelSelectionEditor GetSelectionEditor()
    {
        if (DrawingLayer is IPixelSelectionEditor editor)
            return editor;
        throw new InvalidOperationException("DrawingLayer does not implement IPixelSelectionEditor");
    }

    public void SelectAll()
    {
        if (_drawingLayer != null)
            _drawingLayer.SelectAll();
    }

    public void InvertSelection()
    {
        if (_drawingLayer is not DrawingLayerNode drawingLayerNode || CurrentDrawingTarget == null)
            return;

        if (!drawingLayerNode.HasSelection)
        {
            drawingLayerNode.SelectAll();
            return;
        }

        if (drawingLayerNode.SelectionPhase == SelectionPhase.Transforming)
            drawingLayerNode.SetSelectionTransformMode(false);

        var beforeSelection = (SpriteSelectionNode)drawingLayerNode.GetSelectionLayer();
        var beforeBackground = drawingLayerNode.GetSelectionBackground();
        var toolKey = _appState.ToolsState.CurrentToolKey;

        using var targetSnapshot = new SKBitmap(new SKImageInfo(
            (int)CurrentDrawingTarget.GetSize().Width,
            (int)CurrentDrawingTarget.GetSize().Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul));
        CurrentDrawingTarget.CopyBitmapTo(targetSnapshot);

        var afterSelection = InvertSelectionOperation.CreateInvertedSelectionState(CurrentDrawingTarget, beforeSelection, targetSnapshot);
        var operation = new InvertSelectionOperation(
            drawingLayerNode,
            new SelectionStateSnapshot(beforeSelection, beforeBackground, ContourOnly: true),
            afterSelection,
            toolKey,
            toolKey);

        _operationService.InvokeAndPushOperations(operation);
    }

    public void CancelCurrentOperation() => _operationFactory?.CancelCurrentOperation();

    public void CancelActiveDrawing() => _operationFactory?.CancelActiveDrawing();

    public void CommitTransformWithUndo(bool keepMarqueeInContour, string? toolKeyBefore, string? toolKeyAfter)
    {
        // Snapshot phase: capture everything we need BEFORE the commit, since either path below can drop the
        // selection layer / background bitmap and we won't be able to re-read them off the layer after that.
        if (_drawingLayer is not DrawingLayerNode dln) return;
        if (CurrentDrawingTarget == null) return;
        if (dln.SelectionPhase != SelectionPhase.Transforming) return;

        var targetDataBefore = CurrentDrawingTarget.GetData();
        var selectionLayer = (SpriteSelectionNode)dln.GetSelectionLayer();
        var backgroundBitmap = dln.GetSelectionBackground();

        if (keepMarqueeInContour)
            dln.SetSelectionTransformMode(false);
        else
            dln.ApplySelection();

        var targetDataAfter = CurrentDrawingTarget.GetData();

        var op = new ApplyTransformOperation(
            CurrentDrawingTarget,
            dln,
            selectionLayer,
            backgroundBitmap,
            targetDataBefore,
            targetDataAfter,
            keepMarqueeInContour,
            toolKeyBefore,
            toolKeyAfter);
        _operationService.PushOperations(op);
    }

    public void Refresh() => _viewPortRefreshService.Refresh();
}