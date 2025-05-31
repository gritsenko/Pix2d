using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Plugins.Sprite.Commands;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaSharp;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.Sprite;

//prevent from being trimmed by AOT compiler
[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(SpritePlugin))]
public class SpritePlugin(ICommandService commandService, IDrawingService drawingService)
    : IPix2dPlugin
{

    public static SpriteAnimationCommands AnimationCommands { get; } = new();
    public static SpriteEditCommands EditCommands { get; } = new();

    public void Initialize()
    {
        commandService.RegisterCommandList(EditCommands);
        commandService.RegisterCommandList(AnimationCommands);
    }

    internal (IEnumerable<SKNode> Nodes, SKColor BackgroundColor) GetDataForCutOrCopy(AppState appState)
    {
        if (appState.ToolsState.CurrentTool.ToolInstance is not IPixelSelectionTool)
            return ([], SKColor.Empty);

        IEnumerable<SKNode> selectedNodes = appState.CurrentProject.Selection?.Nodes;
        if (drawingService.DrawingLayer.HasSelection)
        {
            var tmpSprite = new BitmapNode()
            { IsVisible = true, Bitmap = ((BitmapNode)drawingService.DrawingLayer.GetSelectionLayer()).Bitmap };
            selectedNodes = tmpSprite.Yield();
        }
        return (selectedNodes, SKColor.Empty);
    }

    public void FillSelection(SKColor color)
    {
        if (drawingService.DrawingLayer != null)
        {
            drawingService.DrawingLayer.FillSelection(color);
        }
    }
}