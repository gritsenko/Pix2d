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
        SpriteEditorState.WatchFor(x => x.Symmetry, ApplySymmetry);
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
        _drawingLayer.Symmetry = SpriteEditorState.Symmetry;

        if (_drawingLayer is DrawingLayerNode symmetryHost)
            symmetryHost.SymmetryCenterChanged = SetSymmetryCenter;

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

        // Non-null only for a Shift/Ctrl combining marquee — undo then steps back to the selection this one
        // grew out of rather than clearing the lot.
        var op = new BeginSelectionOperation(dln, selectionLayer, backgroundBitmap, toolKey, dln.LastCombinedFromSelection);
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
    /// The shipped preset row, minus whatever the user has removed (see <see cref="DeleteBrushPreset"/> and
    /// <see cref="ResetBrushPresetsToDefaults"/>). Deliberately not persisted itself: keeping built-ins in code
    /// means a later release can change their scale/opacity freely — only the stable <see cref="BrushSettings.BuiltInId"/>
    /// of a removed row is ever written to disk.
    /// </summary>
    private List<BrushSettings> BuildBuiltInPresets()
    {
        var all = new List<BrushSettings>
        {
            new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 1, Opacity = 1f, BuiltInId = "square-1" },
            new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 2, Opacity = 1f, BuiltInId = "square-2" },
            new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 3, Opacity = 1f, BuiltInId = "square-3" },
            new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 4, Opacity = 1f, BuiltInId = "square-4" },
            new() { Brush = GetBrush<SquareSolidBrush>(), Scale = 5, Opacity = 1f, BuiltInId = "square-5" },
            new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 4, Opacity = 1f, BuiltInId = "circle-4" },
            new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 6, Opacity = 1f, BuiltInId = "circle-6" },
            new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 8, Opacity = 1f, BuiltInId = "circle-8" },
            new() { Brush = GetBrush<CircleSolidBrush>(), Scale = 10, Opacity = 1f, BuiltInId = "circle-10" },
            new() { Brush = GetBrush<SprayBrush>(), Scale = 16, Opacity = 0.1f, BuiltInId = "spray-16" },
            new() { Brush = GetBrush<MarkerBrush>(), Scale = 16, Opacity = 0.5f, BuiltInId = "marker-16" }
        };

        var hidden = GetHiddenBuiltInIds();
        return hidden.Count == 0 ? all : all.Where(p => !hidden.Contains(p.BuiltInId!)).ToList();
    }

    #region user brush presets

    // = nameof(AppSettings.UserBrushPresets). SettingsService resolves keys to AppSettings properties by
    // reflection and an undeclared key is a silently-dropped write, so this string and that property must
    // stay in lockstep (the trap that cost a release in the rate-prompt funnel).
    private const string UserBrushPresetsSettingKey = "UserBrushPresets";

    // = nameof(AppSettings.HiddenBuiltInPresetIds). Same lockstep requirement as above.
    private const string HiddenBuiltInPresetIdsSettingKey = "HiddenBuiltInPresetIds";

    // A captured selection this big already covers any pixel-art motif worth stamping; capping it keeps
    // AppSettings.UserBrushPresets (re-serialized as a whole file on every write, see SettingsService.Set)
    // from ballooning on a large marquee.
    private const int MaxStampSourceDimension = 128;

    private HashSet<string> GetHiddenBuiltInIds()
    {
        if (_settingsService.TryGet<List<string>>(HiddenBuiltInPresetIdsSettingKey, out var stored) && stored != null)
            return new HashSet<string>(stored, StringComparer.Ordinal);

        return [];
    }

    private void PersistHiddenBuiltInIds(HashSet<string> ids) =>
        _settingsService.Set(HiddenBuiltInPresetIdsSettingKey, ids.ToList());

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
            if (data.Brush == BrushKeys.StampKey)
            {
                var stampPreset = TryLoadStampPreset(data);
                if (stampPreset == null)
                    Logger.Trace("Skipping a saved brush preset with a missing or corrupt stamp image.");
                else
                    result.Add(stampPreset);
                continue;
            }

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

    private static BrushSettings? TryLoadStampPreset(BrushPresetData data)
    {
        if (string.IsNullOrEmpty(data.StampImagePng))
            return null;

        SKBitmap? source;
        try
        {
            source = SKBitmap.Decode(Convert.FromBase64String(data.StampImagePng));
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return null;
        }

        if (source is not { Width: > 0, Height: > 0 })
        {
            source?.Dispose();
            return null;
        }

        // SKBitmap.Decode lands in the platform's native N32 color type (Bgra8888 on Windows/Android), not
        // the Rgba8888 every bitmap the app allocates uses. Skia itself is color-type aware, so the stamp
        // still PAINTS correctly either way — but the preview tile hands its raw bytes to Avalonia, and
        // anything else downstream that assumes the app-wide type would read this one's channels swapped.
        // Normalize once, here, so a restored stamp is indistinguishable from a freshly captured one.
        source = NormalizeColorType(source);

        return new BrushSettings
        {
            Brush = new ImageStampBrush(source, data.StampUseOriginalColors),
            Scale = Math.Clamp(data.Scale, 1f, 512f),
            Opacity = Math.Clamp(data.Opacity, 0f, 1f),
            Spacing = Math.Clamp(data.Spacing, 0.01f, 10f),
            PressureAffectsSize = data.PressureAffectsSize,
            PressureAffectsOpacity = data.PressureAffectsOpacity,
            IsUserPreset = true
        };
    }

    /// <summary>Returns <paramref name="source"/> unchanged when it already is in
    /// <see cref="Pix2DAppSettings.ColorType"/>, otherwise a converted copy (the original is disposed).
    /// A failed conversion falls back to the original — a stamp with swapped channels still beats no stamp.
    /// </summary>
    private static SKBitmap NormalizeColorType(SKBitmap source)
    {
        if (source.ColorType == Pix2DAppSettings.ColorType)
            return source;

        var converted = source.Copy(Pix2DAppSettings.ColorType);
        if (converted == null)
            return source;

        source.Dispose();
        return converted;
    }

    public BrushSettings? SaveCurrentBrushAsPreset()
    {
        var current = SpriteEditorState.CurrentBrushSettings;
        if (current?.Brush == null)
            return null;
        if (current.Brush is not ImageStampBrush && BrushKeys.GetKey(current.Brush) == null)
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

    /// <summary>
    /// Captures the current pixel selection into a brand-new preset (an <see cref="ImageStampBrush"/> owning a
    /// copy of the selected pixels) and appends it to the row. Returns null when there is no active selection,
    /// or the selection has no pixels to capture.
    /// </summary>
    public BrushSettings? CreateBrushPresetFromSelection(bool useOriginalColors)
    {
        if (!DrawingLayer.HasSelection)
            return null;

        if (DrawingLayer.GetSelectionLayer() is not BitmapNode selectionNode ||
            selectionNode.Bitmap is not { Width: > 0, Height: > 0 } sourceBitmap)
            return null;

        var captured = sourceBitmap.Copy();

        var longSide = Math.Max(captured.Width, captured.Height);
        if (longSide > MaxStampSourceDimension)
        {
            var factor = MaxStampSourceDimension / (float)longSide;
            var w = Math.Max(1, (int)Math.Round(captured.Width * factor));
            var h = Math.Max(1, (int)Math.Round(captured.Height * factor));

            var downscaled = captured.Resize(new SKSizeI(w, h), new SKSamplingOptions(SKFilterMode.Nearest));
            if (downscaled != null)
            {
                captured.Dispose();
                captured = downscaled;
            }
        }

        var preset = new BrushSettings
        {
            Brush = new ImageStampBrush(captured, useOriginalColors),
            Scale = Math.Clamp(Math.Max(captured.Width, captured.Height), 1, 512),
            Opacity = 1f,
            Spacing = 1f,
            IsUserPreset = true
        };

        SpriteEditorState.BrushPresets = [.. SpriteEditorState.BrushPresets, preset];
        PersistUserPresets();

        return preset;
    }

    /// <summary>
    /// Removes a preset from the row and persists that removal. A user preset is dropped outright; a built-in
    /// is only hidden (its <see cref="BrushSettings.BuiltInId"/> is remembered in
    /// <c>AppSettings.HiddenBuiltInPresetIds</c>) so <see cref="ResetBrushPresetsToDefaults"/> can bring it back.
    /// </summary>
    public bool DeleteBrushPreset(BrushSettings preset)
    {
        var presets = SpriteEditorState.BrushPresets;

        // By reference, NOT by value: BrushSettings has value equality, so List.Remove would happily delete a
        // built-in that happens to have identical settings to some other preset.
        var index = presets.FindIndex(p => ReferenceEquals(p, preset));
        if (index < 0)
            return false;

        var updated = new List<BrushSettings>(presets);
        updated.RemoveAt(index);
        SpriteEditorState.BrushPresets = updated;

        if (preset.IsUserPreset)
        {
            PersistUserPresets();
        }
        else if (preset.BuiltInId != null)
        {
            var hidden = GetHiddenBuiltInIds();
            hidden.Add(preset.BuiltInId);
            PersistHiddenBuiltInIds(hidden);
        }

        // CurrentBrushSettings is a clone, so the user keeps drawing with the same brush; only the row's
        // highlight needs clearing when the deleted tile was the selected one.
        if (ReferenceEquals(SpriteEditorState.CurrentPixelBrushPreset, preset))
            SpriteEditorState.CurrentPixelBrushPreset = null!;

        return true;
    }

    /// <summary>
    /// Restores every built-in preset the user has removed. Never touches <c>AppSettings.UserBrushPresets</c> —
    /// presets the user actually saved (plain or stamp) are kept, in place, by reference, so a currently
    /// selected one stays selected.
    /// </summary>
    public void ResetBrushPresetsToDefaults()
    {
        PersistHiddenBuiltInIds([]);

        var existingUserPresets = SpriteEditorState.BrushPresets.Where(p => p.IsUserPreset).ToList();
        SpriteEditorState.BrushPresets = [.. BuildBuiltInPresets(), .. existingUserPresets];

        if (SpriteEditorState.CurrentPixelBrushPreset != null &&
            !SpriteEditorState.BrushPresets.Any(p => ReferenceEquals(p, SpriteEditorState.CurrentPixelBrushPreset)))
        {
            SpriteEditorState.CurrentPixelBrushPreset = null!;
        }
    }

    private void PersistUserPresets()
    {
        var data = SpriteEditorState.BrushPresets
            .Where(p => p.IsUserPreset)
            .Select(ToPresetData)
            .Where(d => d != null)
            .Select(d => d!)
            .ToList();

        _settingsService.Set(UserBrushPresetsSettingKey, data);
    }

    private static BrushPresetData? ToPresetData(BrushSettings p)
    {
        if (p.Brush is ImageStampBrush stamp)
        {
            using var encoded = stamp.SourceBitmap.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null)
                return null;

            return new BrushPresetData
            {
                Brush = BrushKeys.StampKey,
                Scale = p.Scale,
                Opacity = p.Opacity,
                Spacing = p.Spacing,
                PressureAffectsSize = p.PressureAffectsSize,
                PressureAffectsOpacity = p.PressureAffectsOpacity,
                StampImagePng = Convert.ToBase64String(encoded.ToArray()),
                StampUseOriginalColors = stamp.UseOriginalColors
            };
        }

        var key = BrushKeys.GetKey(p.Brush);
        if (string.IsNullOrEmpty(key))
            return null;

        return new BrushPresetData
        {
            Brush = key,
            Scale = p.Scale,
            Opacity = p.Opacity,
            Spacing = p.Spacing,
            PressureAffectsSize = p.PressureAffectsSize,
            PressureAffectsOpacity = p.PressureAffectsOpacity
        };
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
        // SetTarget may have resized the layer to a different canvas; a centre the user moved is clamped
        // into the new one on read, but the axes still have to be re-pushed and repainted.
        ApplySymmetry();
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

    public void SetSymmetry(SymmetrySettings settings) => SpriteEditorState.Symmetry = settings;

    public void SetSymmetryCenter(SKPoint? center) =>
        SpriteEditorState.Symmetry = SpriteEditorState.Symmetry with { Center = center };

    /// <summary>
    /// Pushes the state's symmetry into the drawing layer and repaints. Called from the state watcher and
    /// from every point the drawing layer or its target is (re)attached — the layer is replaced/retargeted
    /// on project load, tab switch and artboard switch, and a setting the user turned on must survive that.
    /// </summary>
    private void ApplySymmetry()
    {
        if (_drawingLayer == null)
            return;

        _drawingLayer.Symmetry = SpriteEditorState.Symmetry;
        Refresh();
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

    public void SelectOpaquePixels(SKBitmap? maskSource)
    {
        _drawingLayer?.SelectOpaquePixels(maskSource);
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