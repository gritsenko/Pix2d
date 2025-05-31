using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Platform;
using Pix2d.CommonNodes;
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

    private SpriteEditor SpriteEditor => AppState.CurrentProject.CurrentNodeEditor as SpriteEditor;
    private IDrawingService DrawingService => ServiceProvider.GetRequiredService<IDrawingService>();

    public Pix2dCommand CopyPixels =>
        GetCommand(() =>
        {
            var (nodes, backgroundColor) = ServiceProvider.GetRequiredService<SpritePlugin>().GetDataForCutOrCopy(AppState);
            ServiceProvider.GetRequiredService<IClipboardService>().TryCopyNodesAsBitmapAsync(nodes, backgroundColor);
        }, "Copy selected pixels", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand CopyMerged => GetCommand(() =>
    {
        ServiceProvider.GetRequiredService<IDrawingService>().CancelCurrentOperation();
        var container = ServiceProvider.GetRequiredService<ISelectionService>().GetActiveContainer();
        ServiceProvider.GetRequiredService<IClipboardService>().TryCopyNodesAsBitmapAsync(container.Yield().OfType<SKNode>(), container.BackgroundColor);
    }, "Copy multiple layers", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand CutPixels =>
        GetCommand(() =>
        {
            var (nodes, backgroundColor) = ServiceProvider.GetRequiredService<SpritePlugin>().GetDataForCutOrCopy(AppState);
            ServiceProvider.GetRequiredService<IClipboardService>().TryCutNodesAsBitmapAsync(nodes, backgroundColor);
        }, "Cut selected pixels", new CommandShortcut(VirtualKeys.X, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand TryPaste => GetCommand(() => { ServiceProvider.GetRequiredService<IClipboardService>().TryPaste(); }, "Paste pixels", new CommandShortcut(VirtualKeys.V, KeyModifier.Ctrl), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand CropPixels =>
        GetCommand(async () =>
        {
            SKBitmap selectionBitmap = null;
            SKRect targetBounds = default;

            var drawingLayer = ServiceProvider.GetRequiredService<IDrawingService>().DrawingLayer;
            var selectionLayer = drawingLayer.GetSelectionLayer();
            if (selectionLayer is BitmapNode bmn)
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
        GetCommand(() => { SpriteEditor.Flip(FlipMode.Horizontal); }, "Flip Horizontal", new CommandShortcut(VirtualKeys.H, KeyModifier.Shift), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand FlipVertical =>
        GetCommand(() => { SpriteEditor.Flip(FlipMode.Vertical); }, "Flip Vertical", new CommandShortcut(VirtualKeys.V, KeyModifier.Shift), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

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
                SpriteEditor.RotateCurrentFrame();
            }
        }, "Rotate 90°", new CommandShortcut(VirtualKeys.R, KeyModifier.Shift), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Rotate90All => GetCommand(() => SpriteEditor.RotateSprite(), "Rotate all 90°", new CommandShortcut(VirtualKeys.R, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);


    public Pix2dCommand Clear => GetCommand(() => { ServiceProvider.GetRequiredService<IDrawingService>().ClearCurrentLayer(); },
        "Clear pixels", new CommandShortcut(VirtualKeys.Delete), EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Cancel => GetCommand(() => { ServiceProvider.GetRequiredService<IDrawingService>().CancelCurrentOperation(); }, "Cancel drawing", new CommandShortcut(VirtualKeys.Escape), EditContextType.Sprite);

    public Pix2dCommand ApplySelection => GetCommand(() => { ServiceProvider.GetRequiredService<IDrawingService>().DrawingLayer.ApplySelection(); }, "Apply selection", new CommandShortcut(VirtualKeys.Return), EditContextType.Sprite);

    public Pix2dCommand SendLayerBackward =>
        GetCommand(() => { SpriteEditor.SendLayerBackward(); }, "Send current layer backward", new CommandShortcut(VirtualKeys.OEM4, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand BringLayerForward =>
        GetCommand(() => { SpriteEditor.BringLayerForward(); }, "Bring current layer forward", new CommandShortcut(VirtualKeys.OEM6, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand AddLayer =>
        GetCommand(() => { SpriteEditor.AddEmptyLayer(); }, "Add new layer", new CommandShortcut(VirtualKeys.N, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand DeleteLayer =>
        GetCommand(() => { SpriteEditor.DeleteLayer(); }, "Delete current layer", new CommandShortcut(VirtualKeys.Delete, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

    public Pix2dCommand DuplicateLayer =>
        GetCommand(() => { SpriteEditor.DuplicateLayer(); }, "Duplicate current layer", new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);
    public Pix2dCommand MergeLayer =>
        GetCommand(() => { SpriteEditor.MergeDownLayer(); }, "Merge down current layer to bottom neighbor", new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite);

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