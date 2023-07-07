using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Commands;
using Pix2d.CommonNodes;
using Pix2d.Drawing.Tools;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using Pix2d.Primitives.Edit;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Commands;

public class SpriteEditCommands : CommandsListBase
{
    protected override string BaseName => "Sprite.Edit";

    private static SpriteEditor SpriteEditor => CoreServices.EditService.GetCurrentEditor() as SpriteEditor;

    public Pix2dCommand CopyPixels =>
        GetCommand("Copy selected pixels", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl),
            EditContextType.Sprite,
            () =>
            {
                var (nodes, backgroundColor) = SpritePlugin.GetDataForCutOrCopy(AppState);
                CoreServices.ClipboardService.TryCopyNodesAsBitmapAsync(nodes, backgroundColor);
            });

    public Pix2dCommand CopyMerged => GetCommand("Copy active container node merged",
        new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.Sprite,
        () =>
        {
            var container = CoreServices.SelectionService.GetActiveContainer();
            CoreServices.ClipboardService.TryCopyNodesAsBitmapAsync(container.Yield().OfType<SKNode>(), container.BackgroundColor);
        });

    public Pix2dCommand CutPixels =>
        GetCommand("Cut selected pixels", new CommandShortcut(VirtualKeys.X, KeyModifier.Ctrl),
            EditContextType.Sprite,
            () =>
            {
                var (nodes, backgroundColor) = SpritePlugin.GetDataForCutOrCopy(AppState);
                CoreServices.ClipboardService.TryCutNodesAsBitmapAsync(nodes, backgroundColor);
            });

    public Pix2dCommand CropPixels =>
        GetCommand("Crop current sprite", new CommandShortcut(VirtualKeys.K, KeyModifier.Ctrl), EditContextType.Sprite,
            async () =>
            {
                SKBitmap selectionBitmap = null;
                SKRect targetBounds = default;

                var selectiionLayer = CoreServices.DrawingService.DrawingLayer.GetSelectionLayer();
                if (selectiionLayer is BitmapNode bmn)
                {
                    selectionBitmap = bmn.Bitmap.Copy();
                }

                if (AppState.CurrentProject.CurrentTool is PixelSelectTool ps)
                {
                    targetBounds = ps.GetSelectionRect();
                    ps.ApplySelection();
                }

                if (targetBounds != default)
                {
                    CoreServices.EditService.CropCurrentSprite(targetBounds);

                    if (selectionBitmap != null)
                        CoreServices.DrawingService.DrawingLayer?.SetSelectionFromExternal(selectionBitmap, SKPoint.Empty);

                    CoreServices.ViewPortService.ShowAll();
                    await Task.Delay(300);
                    CoreServices.ViewPortService.Refresh();
                }
            });

    public Pix2dCommand FlipHorizontal =>
        GetCommand("Flip Horizontal", new CommandShortcut(VirtualKeys.H, KeyModifier.Shift), EditContextType.Sprite,
            () => { SpriteEditor.Flip(FlipMode.Horizontal); });

    public Pix2dCommand FlipVertical =>
        GetCommand("Flip Vertical", new CommandShortcut(VirtualKeys.V, KeyModifier.Shift), EditContextType.Sprite,
            () => { SpriteEditor.Flip(FlipMode.Vertical); });

    public Pix2dCommand Rotate90 =>
        GetCommand("Rotate 90°", new CommandShortcut(VirtualKeys.R, KeyModifier.Shift), EditContextType.Sprite,
            () => { SpriteEditor.Rotate(90); });


    public Pix2dCommand TryPaste => GetCommand("Paste pixels", new CommandShortcut(VirtualKeys.V, KeyModifier.Ctrl),
        EditContextType.Sprite,
        () => { CoreServices.ClipboardService.TryPaste(); });

    public Pix2dCommand Clear => GetCommand("Clear pixels", new CommandShortcut(VirtualKeys.Delete),
        EditContextType.Sprite,
        () => { CoreServices.DrawingService.CancelCurrentOperation(); });

    public Pix2dCommand Cancel => GetCommand("Cancel drawing", new CommandShortcut(VirtualKeys.Escape),
        EditContextType.Sprite,
        () => { CoreServices.DrawingService.CancelCurrentOperation(); });

    public Pix2dCommand ApplySelection => GetCommand("Apply selection", new CommandShortcut(VirtualKeys.Return),
        EditContextType.Sprite,
        () => { CoreServices.DrawingService.DrawingLayer.ApplySelection(); });

    public Pix2dCommand SendLayerBackward =>
        GetCommand("Send current layer backward", new CommandShortcut(VirtualKeys.OEM4, KeyModifier.Ctrl), EditContextType.Sprite,
            () => { SpriteEditor.SendLayerBackward(); });
    public Pix2dCommand BringLayerForward =>
        GetCommand("Bring current layer forward", new CommandShortcut(VirtualKeys.OEM6, KeyModifier.Ctrl), EditContextType.Sprite,
            () => { SpriteEditor.BringLayerForward(); });

    public Pix2dCommand FillSelectionCommand =>
        GetCommand("Fill selection with current color", new CommandShortcut(VirtualKeys.F, KeyModifier.Shift), EditContextType.Sprite,
            () =>
            {
                SpritePlugin.FillSelection(AppState.DrawingState.CurrentColor);
            });

    public Pix2dCommand SelectObjectCommand =>
        GetCommand("Extract object from image (Using online service)", new CommandShortcut(VirtualKeys.O, KeyModifier.Shift | KeyModifier.Ctrl), EditContextType.Sprite,
            SpritePlugin.SelectObject);
}