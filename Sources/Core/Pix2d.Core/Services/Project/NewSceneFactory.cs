#nullable enable
using System.Collections.Immutable;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.CommonNodes;
using Pix2d.Project;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services.Project;

internal class NewSceneFactory(Action<ImportData> importAction) : IImportTarget
{
    public void Import(ImportData data) => importAction.Invoke(data);

    public static Task<SKNode> GetNewSceneFromFiles(IReadOnlyList<IFileContentSource> files, IImportService importService) =>
        files.First().Extension switch
        {
            ".pix2d" => ProjectUnpacker.LoadProjectScene(files.First()),
            _ => ImportToNewScene(files, importService)
        };

    private static async Task<SKNode> ImportToNewScene(IEnumerable<IFileContentSource> files, IImportService importService)
    {
        SKNode? scene = null;
        var factory = new NewSceneFactory(importData =>
        {
            scene = GetNewScene(importData.Size);
            var sprite = (Pix2dSprite)scene.Nodes[0];
            var layersData = importData.Layers.ToImmutableList();

            for (var i = 0; i < layersData.Count; i++)
            {
                var layerPropertiesInfo = layersData[i];
                var layer = i == 0 
                    ? sprite.Layers.First() //we always have first layer
                    : sprite.AddLayer();

                if (importData.ReplaceFrames)
                    layer.DeleteFrame(0);

                for (var frameIndex = 0; frameIndex < layerPropertiesInfo.Frames.Count; frameIndex++)
                    layer.InsertFrameFromBitmap(
                        frameIndex,
                        layerPropertiesInfo.Frames[frameIndex].BitmapProviderFunc());
            }
        });

        await importService.ImportAsync(files, factory);
        if (scene == null)
            throw new ArgumentException("Scene must not be null");

        return scene;
    }

    public static SKNode GetNewScene(SKSize newProjectSize)
    {
        var scene = new SKNode() { Name = "Scene" };
        var sprite = Pix2dSprite.CreateEmpty(newProjectSize);
        sprite.Name = "New Sprite";
        sprite.DesignerState.IsSelected = true;
        scene.Nodes.Add(sprite);
        return scene;
    }
}