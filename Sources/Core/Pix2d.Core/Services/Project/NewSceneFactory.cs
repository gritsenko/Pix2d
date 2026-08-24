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

    public static async Task<SKNode> GetNewSceneFromFiles(IReadOnlyList<IFileContentSource> files, IImportService importService) =>
        files.First().Extension switch
        {
            ".pix2d" => (await ProjectUnpacker.LoadProjectScene(files.First())) ?? throw new ArgumentException("Failed to load project scene"),
            _ => await ImportToNewScene(files, importService)
        };

    private static async Task<SKNode> ImportToNewScene(IEnumerable<IFileContentSource> files, IImportService importService)
    {
        SKNode? scene = null;
        var factory = new NewSceneFactory(importData =>
        {
            scene = GetNewScene(importData.Size);
            var sprite = scene.Nodes.OfType<Pix2dSprite>().First();
            SpriteImportApplier.Apply(sprite, importData);
        });

        // The import produced nothing. By far the most common cause is a file Pix2d cannot decode at
        // all — Android in particular hands us whatever the file manager offered (Pix2d claims
        // application/octet-stream so its own extensions open at all), so this path is user-facing
        // and must name the reason instead of the old "Scene must not be null".
        var result = await importService.ImportAsync(files, factory);
        if (scene == null)
        {
            var file = files.First();
            var ext = file.Extension;
            var reason = !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : string.IsNullOrWhiteSpace(ext)
                    ? $"Can't tell the file type of \"{file.Title}\"."
                    : $"\"{ext}\" files are not supported. Pix2d opens: {string.Join(", ", importService.SupportedExtensions)}, .pix2d.";
            throw new NotSupportedException(reason);
        }

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