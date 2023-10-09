using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.CommonNodes;
using Pix2d.Exporters;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services;

public class ExportService : IExportService
{
    public ISelectionService SelectionService { get; }
    public ILicenseService LicenseService { get; }
    public IFileService FileService { get; }
    public AppState AppState { get; }

    public IWriteDestinationFolder CurrentBuildFolder { get; set; }
    
    public bool EnableWatermark => LicenseService is { IsPro: false, AllowBuyPro: true };

    public ExportService(ISelectionService selectionService, IFileService fileService, AppState appState, ILicenseService licenseService)
    {
        SelectionService = selectionService;
        FileService = fileService;
        AppState = appState;
        LicenseService = licenseService;
    }


    public IReadOnlyList<IExporter> Exporters { get; } = new List<IExporter>()
    {
        new PngImageExporter(),
        new GifImageExporter(),
        new SpritesheetImageExporter(),
        new SpritePngSequenceExporter(),
        new SvgImageExporter()
    };

    public void ExportSelectedNode()
    {
        throw new NotImplementedException();
        //if (!SelectionService.HasSelectedNodes)
        //    return;

        //var exporter = new TypescriptLayoutExporter();

        //var filename = SelectionService.Selection.Nodes[0].Name.Replace(" ", "");
        //var file = await GetFileToExport(".ts", filename + "Layout");

        //if (file == null)
        //    return;

        //await file.SaveAsync(exporter.Export(SelectionService.Selection.Nodes));
    }

    public async Task ExportNodesAsync(IEnumerable<SKNode> nodesToRender, double scale, IExporter exporter)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(AppState.CurrentProject.Title);

        //on android there is an Issue: If file with the same name already exists, save file picker generates incorrect suggested filename (adds suffix to extesion) so this file will not have valid extension
        //so we add timestamp to filename
        if (Pix2DApp.CurrentPlatform == PlatformType.Android)
            fileName += "_" + DateTime.Now.ToString("s").Replace(":", "").Replace("-", "");

        await exporter.ExportAsync(nodesToRender, scale);
    }

    public async Task ExportNodesToFileAsync(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender, double scale)
    {
        var pngExporter = new PngImageExporter();
        await using var pngStream = await pngExporter.ExportToStreamAsync(nodesToRender);
        await fileContentSource.SaveAsync(pngStream);
    }

    //        public async void ExportAssets()
    //        {
    //            var folder = await FileService.GetFolderToExportWithDialogAsync();
    //            if (folder != null)
    //            {
    ////                var exporter = new AssetExporter(folder);
    //                var exporter = new AtlasAssetExporter(folder);
    //                await exporter.ExportAssets(Service.GetCurrentScene());
    //            }
    //        }

    private async Task<IFileContentSource> GetFileToExport(string filetype, string defaultName = null)
    {
        return await FileService.GetFileToSaveWithDialogAsync(new []{filetype}, "export", defaultName);
    }
    
    public IEnumerable<SKNode> GetNodesToExport(double scale)
    {
        if (CoreServices.EditService.CurrentEditedNode == null)
            yield break;

        yield return CoreServices.EditService.CurrentEditedNode;

        if (CoreServices.EditService.CurrentEditedNode.Size.Width * scale < 64)
        {
            yield break;
        }

        if (EnableWatermark) yield return GetWatermarkNode(CoreServices.EditService.CurrentEditedNode, scale);
    }

    private SKNode GetWatermarkNode(SKNode exportedNode, double scale)
    {
        var watermarkNode = new Pix2dWatermarkNode(StaticResources.WatermarkBitmap);

        var exportedNodeSize = exportedNode.Size;

        var w = exportedNodeSize.Width * scale;
        var h = exportedNodeSize.Height * scale;
        if (w >= 200 && h >= 200)
        {
            watermarkNode.Size = new SKSize((float)(64f / scale), (float)(64f / scale));

            var offset = 8f / scale;
            watermarkNode.Position = new SKPoint((float)(exportedNodeSize.Width - watermarkNode.Size.Width - offset), (float)(exportedNodeSize.Height - watermarkNode.Size.Height - offset));
        }
        else
        {
            watermarkNode.FontSize = (float)(14f / (scale == 0 ? 1 : scale));
            watermarkNode.Size = new SKSize(exportedNodeSize.Width, watermarkNode.FontSize + 2f);
            watermarkNode.Position = new SKPoint(exportedNode.Position.X, exportedNode.Position.Y + exportedNodeSize.Height);
            watermarkNode.Effects = null;
        }

        return watermarkNode;
    }

    //public async void BuildProject()
    //{
    //    CurrentBuildFolder = await FileService.GetFolderToExportWithDialogAsync("buildProject");

    //    if (CurrentBuildFolder != null)
    //    {
    //        var assetsFolder = CurrentBuildFolder.GetSubfolder("assets");

    //        var texturesDir = assetsFolder.GetSubfolder("textures");
    //        var atlasAssetExporter = new AtlasAssetExporter(texturesDir);
    //        await atlasAssetExporter.ExportAssets(Service.GetCurrentScene());
    //        var textures = atlasAssetExporter.GetExportedTextures();
    //        var atlases = atlasAssetExporter.GetAtlases();


    //        var nodes = new[] {Service.GetCurrentScene()};
    //        var spriteSheet = BuildSpriteSheet(nodes, textures, atlases);

    //        var spritesheetFile = await texturesDir.GetFileSourceAsync("spritesheet", ".json", true);
    //        await spritesheetFile.SaveAsync(spriteSheet);

    //    }

    //}

    //private string BuildSpriteSheet(SKNode[] nodes, Dictionary<string, TextureInfo> textures, Atlas[] atlases)
    //{
    //    var spriteSheet = new SpriteSheet();

    //    var allNodes = nodes.SelectMany(x => x.GetDescendants(null, false, true)).OfType<AnimatedSprite>();

    //    foreach (var animatedSprite in allNodes)
    //    {
    //        var animName = animatedSprite.Name;
    //        var animFrames = new List<string>();
    //        foreach (var spriteNode in animatedSprite.Nodes.OfType<SpriteNode>())
    //        {
    //            var texKey = spriteNode.DesignerState.ExportSettings.TextureKey;
    //            if (textures.TryGetValue(texKey, out var texture))
    //            {
    //                var frame = new SpriteSheetFrame()
    //                {
    //                    frame = new Rect()
    //                    {
    //                        x = texture.X,
    //                        y = texture.Y,
    //                        w = texture.Width,
    //                        h = texture.Height
    //                    },
    //                    anchor = new Anchor(),
    //                    rotated = false,
    //                    trimmed = false,
    //                    sourceSize = new Sourcesize()
    //                    {
    //                        w = texture.Width,
    //                        h = texture.Height
    //                    },
    //                    spriteSourceSize = new Rect()
    //                    {
    //                        x = 0,
    //                        y = 0,
    //                        w = texture.Width,
    //                        h = texture.Height
    //                    }
    //                };

    //                var key = texKey +".png";
    //                spriteSheet.Frames[key] = frame;
    //                animFrames.Add(key);
    //            }
    //        }

    //        spriteSheet.Animations[animName] = animFrames.ToArray();
    //    }

    //    spriteSheet.Meta = new SpriteSheetMeta()
    //    {
    //        image = "atlas00.png",
    //        version = "1.0",
    //        size = new Sourcesize() { w = 1024, h = 1024}
    //    };

    //    return JsonConvert.SerializeObject(spriteSheet);
    //}

    //private async void BuildPixiProject()
    //{
    //    if (CurrentBuildFolder == null)
    //    {
    //        CurrentBuildFolder = await FileService.GetFolderToExportWithDialogAsync("buildProject");
    //    }

    //    if (CurrentBuildFolder != null)
    //    {
    //        CurrentBuildFolder.CopyTemplateFrom(@"Exporters\TypeScript\Pixi\Template");

    //        var assetsFolder = CurrentBuildFolder.GetSubfolder("assets");

    //        var exporter = new AtlasAssetExporter(assetsFolder.GetSubfolder("textures"));
    //        await exporter.ExportAssets(Service.GetCurrentScene());

    //        var textures = exporter.GetExportedTextures();
    //        var atlases = exporter.GetAtlases();

    //        var scriptsFolder = assetsFolder;
    //        var layoutExporter = new PixiLayoutExporter(textures, atlases);

    //        var nodes = Service.GetCurrentScene().Nodes.ToArray();
    //        if (!nodes.Any(x => x is ArtboardNode))
    //        {
    //            nodes = new[] { Service.GetCurrentScene() };
    //        }

    //        await layoutExporter.ExportToDirectoryAsync(nodes, scriptsFolder);
    //    }
    //}

    //public void RunProject()
    //{
    //    BuildProject();
    //    var platformStuffService = IoC.Get<IPlatformStuffService>();
    //    if (platformStuffService != null)
    //    {
    //        platformStuffService.OpenUrlInBrowser("http://localhost:5500/index.html");
    //    }
    //}
}