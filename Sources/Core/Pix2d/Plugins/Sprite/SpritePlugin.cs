using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Plugins.Sprite.Commands;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite;

public class SpritePlugin : IPix2dPlugin
{
    public ICommandService CommandService { get; }
    public IEditService EditService { get; }
    public IToolService ToolService { get; }
    public static SpriteAnimationCommands AnimationCommands { get; } = new SpriteAnimationCommands();
    public static SpriteEditCommands EditCommands { get; } = new SpriteEditCommands();

    public SpritePlugin(ICommandService commandService, IEditService editService, IToolService toolService)
    {
        CommandService = commandService;
        EditService = editService;
        ToolService = toolService;
    }

    public void Initialize()
    {
        CommandService.RegisterCommandList(EditCommands);
        CommandService.RegisterCommandList(AnimationCommands);

        //EditService.RegisterEditor<Pix2dSprite, SpriteEditor>();
    }

    internal static (IEnumerable<SKNode> Nodes, SKColor BackgroundColor) GetDataForCutOrCopy(AppState appState)
    {
        if (appState.ToolsState.CurrentTool.ToolInstance is not IPixelSelectionTool)
            return (Enumerable.Empty<SKNode>(), SKColor.Empty);
        
        IEnumerable<SKNode> selectedNodes = appState.CurrentProject.Selection?.Nodes;
        if (CoreServices.DrawingService.DrawingLayer.HasSelection)
        {
            var tmpSprite = new BitmapNode()
                { IsVisible = true, Bitmap = ((BitmapNode)CoreServices.DrawingService.DrawingLayer.GetSelectionLayer()).Bitmap };
            selectedNodes = tmpSprite.Yield();
        }
        return (selectedNodes, SKColor.Empty);
    }

    public static void FillSelection(SKColor color)
    {
        if (CoreServices.DrawingService.DrawingLayer != null)
        {
            CoreServices.DrawingService.DrawingLayer.FillSelection(color);
        }
    }

    internal static async Task SelectObject()
    {
        if (CoreServices.DrawingService.DrawingLayer == null)
        {
            return;
        }

        var selectionLayer = CoreServices.DrawingService.DrawingLayer.GetSelectionLayer();
        if (selectionLayer is BitmapNode bmn)
        {
            var bitmap = bmn.Bitmap;

            var img = await GetBitmapWithRemovedBackground(bitmap);
            CoreServices.DrawingService.PasteBitmap(img, SKPoint.Empty);
            CoreServices.ViewPortService.Refresh();

        }
    }

    private async static Task<SKBitmap> GetBitmapWithRemovedBackground(SKBitmap bitmapToProcess)
    {
        using var multipartFormContent = new MultipartFormDataContent();
        using var httpClient = new HttpClient();
        //var url = "http://my.gritsenko.biz:5000/?url=https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Vladimir_Putin_17-11-2021_%28cropped%29.jpg/250px-Vladimir_Putin_17-11-2021_%28cropped%29.jpg";
        var url = "http://my.gritsenko.biz:5000/";

        //Load the file and set the file's Content-Type header
        var fileStreamContent = new StreamContent(bitmapToProcess.ToPngStream());
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipartFormContent.Add(fileStreamContent, name: "file", fileName: "image.png");
        var response = await httpClient.PostAsync(url, multipartFormContent);
        response.EnsureSuccessStatusCode();
        
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return bytes.ToSKBitmap();
    }
}
