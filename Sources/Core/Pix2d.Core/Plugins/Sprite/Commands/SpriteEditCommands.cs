#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Platform;
using Pix2d.CommonNodes;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using Pix2d.Primitives.Edit;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Commands;

public class SpriteEditCommands : CommandsListBase, ISpriteEditCommands
{
    protected override string BaseName => "Sprite.Edit";

    private SpriteEditor? SpriteEditor => AppState.CurrentProject.CurrentNodeEditor as SpriteEditor;
    private IDrawingService DrawingService => ServiceProvider.GetRequiredService<IDrawingService>();
    private IToolService ToolService => ServiceProvider.GetRequiredService<IToolService>();

    private bool IsTransformToolActive() => AppState.ToolsState.CurrentToolKey == nameof(PixelTransformTool);

    private void ActivateReturnSelectionTool()
    {
        var toolKey = AppState.SelectionState.ReturnSelectionToolKey;
        if (!ToolService.IsSelectionTool(toolKey) || toolKey == nameof(PixelTransformTool))
            toolKey = nameof(PixelSelectRectTool);

        var toolType = AppState.ToolsState.Tools.FirstOrDefault(x => x.Name == toolKey)?.ToolType;
        if (toolType != null)
            ToolService.ActivateTool(toolType);
        else
            ToolService.ActivateTool<PixelSelectRectTool>();
    }

    public Pix2dCommand CopyPixels =>
        GetCommand(() =>
        {
            var (nodes, backgroundColor) = ServiceProvider.GetRequiredService<SpritePlugin>().GetDataForCutOrCopy(AppState);
            ServiceProvider.GetRequiredService<IClipboardService>().TryCopyNodesAsBitmapAsync(nodes, backgroundColor);
        }, "Copy selected pixels", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl), EditContextType.Sprite,
            behaviour: ServiceProvider.GetRequiredService<EnableOnClipboardSelectionCommandBehavior>());

    public Pix2dCommand CopyMerged => GetCommand(() =>
    {
        ServiceProvider.GetRequiredService<IDrawingService>().CancelCurrentOperation();
        var container = ServiceProvider.GetRequiredService<ISelectionService>().GetActiveContainer();
        if (container == null)
            return;

        ServiceProvider.GetRequiredService<IClipboardService>().TryCopyNodesAsBitmapAsync(container.Yield().OfType<SKNode>(), container.BackgroundColor);
    }, "Copy multiple layers", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand CutPixels =>
        GetCommand(() =>
        {
            var (nodes, backgroundColor) = ServiceProvider.GetRequiredService<SpritePlugin>().GetDataForCutOrCopy(AppState);
            ServiceProvider.GetRequiredService<IClipboardService>().TryCutNodesAsBitmapAsync(nodes, backgroundColor);
        }, "Cut selected pixels", new CommandShortcut(VirtualKeys.X, KeyModifier.Ctrl), EditContextType.Sprite,
            behaviour: ServiceProvider.GetRequiredService<EnableOnClipboardSelectionCommandBehavior>());

    public Pix2dCommand TryPaste => GetCommand(() => { ServiceProvider.GetRequiredService<IClipboardService>().TryPaste(); }, "Paste pixels", new CommandShortcut(VirtualKeys.V, KeyModifier.Ctrl), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

     public Pix2dCommand CropPixels =>
         GetCommand(async () =>
         {
             SKBitmap? selectionBitmap = null;
             SKRect targetBounds = default;

             var drawingLayer = ServiceProvider.GetRequiredService<IDrawingService>().DrawingLayer;
             var selectionLayer = drawingLayer.GetSelectionLayer();
             if (selectionLayer is BitmapNode bmn && bmn.Bitmap != null)
             {
                 selectionBitmap = bmn.Bitmap.Copy();
             }

             targetBounds = selectionLayer?.GetBoundingBox() ?? default;

             drawingLayer.ApplySelection();

             if (targetBounds != default)
             {
                 ServiceProvider.GetRequiredService<IEditService>().CropCurrentSprite(targetBounds);

                 if (selectionBitmap != null)
                     ServiceProvider.GetRequiredService<IDrawingService>().DrawingLayer?.SetSelectionFromExternal(selectionBitmap, SKPoint.Empty);

                 var sp = ServiceProvider;
                 sp.GetRequiredService<IViewPortService>().ShowAll();
                 await Task.Delay(300);
                 sp.GetRequiredService<IViewPortRefreshService>().Refresh();
             }
         }, "Crop current sprite", new CommandShortcut(VirtualKeys.K, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand FlipHorizontal =>
        GetCommand(() => { SpriteEditor?.Flip(FlipMode.Horizontal); }, "Flip Horizontal", new CommandShortcut(VirtualKeys.H, KeyModifier.Shift), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand FlipVertical =>
        GetCommand(() => { SpriteEditor?.Flip(FlipMode.Vertical); }, "Flip Vertical", new CommandShortcut(VirtualKeys.V, KeyModifier.Shift), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand RotateMinus90 =>
        GetCommand(() =>
        {
            var selectionEditor = DrawingService.GetSelectionEditor();
            if (selectionEditor.HasSelection)
                selectionEditor.RotateSelection(-90);
        }, "Rotate -90°", null, EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

     public Pix2dCommand Rotate90 =>
         GetCommand(() =>
         {
             var selectionEditor = DrawingService.GetSelectionEditor();
             if (selectionEditor.HasSelection)
             {
                 selectionEditor.RotateSelection(90);
             }
             else
             {
                 SpriteEditor?.RotateCurrentFrame();
             }
         }, "Rotate 90°", new CommandShortcut(VirtualKeys.R, KeyModifier.Shift), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Rotate90All => GetCommand(() => SpriteEditor?.RotateSprite(), "Rotate all 90°", new CommandShortcut(VirtualKeys.R, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);


    public Pix2dCommand Clear => GetCommand(() => { ServiceProvider.GetRequiredService<IDrawingService>().ClearCurrentLayer(); },
        "Clear pixels", new CommandShortcut(VirtualKeys.Delete), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Cancel => GetCommand(() =>
    {
        // Esc while placing an artboard as an object cancels that gesture (restore position/size, no operation).
        var artboardObjectEdit = ServiceProvider.GetRequiredService<Pix2d.Services.ArtboardObjectEditService>();
        if (artboardObjectEdit.IsActive)
        {
            artboardObjectEdit.Cancel();
            return;
        }

        if (IsTransformToolActive())
        {
            DrawingService.CancelCurrentOperation();
            ActivateReturnSelectionTool();
            return;
        }

        DrawingService.CancelCurrentOperation();
    }, "Cancel drawing", new CommandShortcut(VirtualKeys.Escape), EditContextType.Sprite);

    public Pix2dCommand ApplySelection => GetCommand(() =>
    {
        if (IsTransformToolActive())
        {
            ActivateReturnSelectionTool();
            return;
        }

        DrawingService.DrawingLayer.ApplySelection();
    }, "Apply selection", new CommandShortcut(VirtualKeys.Return), EditContextType.Sprite);

    public Pix2dCommand ActivateSelectionTransform => GetCommand(() =>
    {
        // PixelTransformTool is the single canonical owner of the Transforming phase — switching to it
        // handles lift / editor mode and undo/redo tool restoration in a consistent way. We keep the
        // legacy "no-op without selection" semantic so an accidental Ctrl+Shift+T doesn't yank the user
        // out of their current tool. (The tool's own fallback to PixelSelectRectTool is meant for the
        // hotkey-on-tool path, not for command-on-empty-selection.)
        if (DrawingService.DrawingLayer.HasSelection)
            ToolService.ActivateTool<PixelTransformTool>();
    }, "Transform selection", new CommandShortcut(VirtualKeys.T, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand SendLayerBackward =>
        GetCommand(() => { SpriteEditor?.SendLayerBackward(); }, "Send current layer backward", new CommandShortcut(VirtualKeys.OEM4, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand BringLayerForward =>
        GetCommand(() => { SpriteEditor?.BringLayerForward(); }, "Bring current layer forward", new CommandShortcut(VirtualKeys.OEM6, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand AddLayer =>
        GetCommand(() => { SpriteEditor?.AddEmptyLayer(); }, "Add new layer", new CommandShortcut(VirtualKeys.N, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand AddArtboard =>
        GetCommand(() =>
        {
            // A new artboard inherits the current sprite's size by default.
            var size = AppState.CurrentProject.CurrentEditedNode?.Size ?? new SKSize(32, 32);
            ServiceProvider.GetRequiredService<IEditService>().AddArtboard(size);
        }, "Add new sprite", new CommandShortcut(VirtualKeys.N, KeyModifier.Ctrl | KeyModifier.Alt), EditContextType.Sprite);

    public Pix2dCommand DeleteLayer =>
        GetCommand(() => { SpriteEditor?.DeleteLayer(); }, "Delete current layer", new CommandShortcut(VirtualKeys.Delete, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand DuplicateLayer =>
        GetCommand(() => { SpriteEditor?.DuplicateLayer(); }, "Duplicate current layer", new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);
    public Pix2dCommand MergeLayer =>
        GetCommand(() => { SpriteEditor?.MergeDownLayer(); }, "Merge down current layer to bottom neighbor", new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand FillSelectionCommand =>
        GetCommand(() =>
        {
            ServiceProvider.GetRequiredService<SpritePlugin>().FillSelection(AppState.SpriteEditorState.CurrentColor);
        }, "Fill selection with current color", new CommandShortcut(VirtualKeys.F, KeyModifier.Shift), EditContextType.Sprite);

    // TODO: This can work, but need to use AiPlugin instead of HTTP service
    // public Pix2dCommand SelectObjectCommand =>
    //     GetCommand("Extract object from image", new CommandShortcut(VirtualKeys.O, KeyModifier.Shift | KeyModifier.Ctrl), EditContextType.Sprite,
    //         SpritePlugin.SelectObject);
}
