#nullable enable
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
public class SpritePlugin(ICommandService commandService, IDrawingService drawingService,
    IToolService toolService,
    Pix2d.Services.ArtboardObjectEditService artboardObjectEditService)
    : IPix2dPlugin
{

    public static SpriteAnimationCommands AnimationCommands { get; } = new();
    public static SpriteEditCommands EditCommands { get; } = new();

    public void Initialize()
    {
        commandService.RegisterCommandList(EditCommands);
        commandService.RegisterCommandList(AnimationCommands);

        // Default (and so far only) tool of the General/objects context: select, drag, band-select,
        // double-click into an artboard. First registration for a context becomes its default tool.
        toolService.RegisterTool<Pix2d.Tools.ObjectManipulationTool>(EditContextType.General);

        // Force-construct the artboard overlay / object-edit service so its message subscriptions are live
        // before the first project loads, and attach the always-on name labels to the current scene.
        artboardObjectEditService.Initialize();
    }

      internal (IEnumerable<SKNode> Nodes, SKColor BackgroundColor) GetDataForCutOrCopy(AppState appState)
      {
          if (appState.ToolsState.CurrentTool?.ToolInstance is not IPixelSelectionTool)
              return ([], SKColor.Empty);

         IEnumerable<SKNode>? selectedNodes = appState.CurrentProject.Selection?.Nodes;
         if (drawingService.DrawingLayer.HasSelection)
         {
             var tmpSprite = new BitmapNode()
             { IsVisible = true, Bitmap = ((BitmapNode)drawingService.DrawingLayer.GetSelectionLayer()).Bitmap };
             selectedNodes = tmpSprite.Yield();
         }
         return (selectedNodes ?? [], SKColor.Empty);
     }

    public void FillSelection(SKColor color)
    {
        if (drawingService.DrawingLayer != null)
        {
            drawingService.DrawingLayer.FillSelection(color);
        }
    }
}