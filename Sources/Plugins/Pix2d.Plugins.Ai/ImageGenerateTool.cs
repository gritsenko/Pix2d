using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Operations.Drawing;
using Pix2d.Plugins.Ai.UI;
using Pix2d.State;
using SkiaSharp;

namespace Pix2d.Plugins.Ai;

[Pix2dTool(
    DisplayName = "Image generation tool",
    HotKey = null,
    HasSettings = true,
    SettingsViewType = typeof(ImageGenerateSettingsView),
    EditContextType = EditContextType.Sprite,
    IconData = AiPlugin.ToolIcon
)]
public class ImageGenerateTool(IDrawingService drawingService, IMessenger messenger, AppState appState)
    : BaseTool, IDrawingTool
{
    private DrawingOperationWithDiffState _pixelSelectDrawingOperation;

    public IDrawingService DrawingService { get; } = drawingService;
    public IMessenger Messenger { get; } = messenger;
    public AppState AppState { get; } = appState;

    private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

    public override async Task Activate()
    {
        _pixelSelectDrawingOperation = null;
        DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.MoveSelection);

        await base.Activate();
    }

    public void GenerateImage(string prompt)
    {
    }

    public override void Deactivate()
    {
        base.Deactivate();
    }

    private SKBitmap BuildBitmap(byte[] data)
    {
        var color = AppState.SpriteEditorState.CurrentColor;
        //var bitmap = new SKNode[] { _textNode }.RenderToBitmap();
        var bitmap = new SKBitmap();
        return bitmap;
    }
}