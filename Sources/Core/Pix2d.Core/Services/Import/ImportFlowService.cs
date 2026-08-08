#nullable enable
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Import.Flow;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.CommonNodes;
using Pix2d.Project;
using Pix2d.UI.Dialogs;
using SkiaSharp;

namespace Pix2d.Services.Import;

/// <summary>
/// Orchestrates the multi-mode import flow. Reuses the existing <see cref="IImportService"/> /
/// <see cref="IImporter"/> pipeline purely as a decode step (file -> bitmaps) via a capturing target,
/// and <see cref="IEditService"/> as the sprite builder. Decides the mode from file kind + drop
/// position, asking the user only when ambiguous.
/// </summary>
public class ImportFlowService(
    AppState appState,
    IImportService importService,
    IEditService editService,
    IProjectService projectService,
    IDialogService dialogService) : IImportFlowService
{
    public async Task<IImportService.ImportResult> RunImportFlowAsync(ImportRequest request)
    {
        if (request.Files == null || request.Files.Count == 0)
            return new IImportService.ImportResult(false, "no files to import");

        try
        {
            var plan = await DecidePlanAsync(request);
            if (plan == null)
                return new IImportService.ImportResult(true); // cancelled by the user

            return await ExecuteAsync(plan, request);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            dialogService.Alert("Can't import file(s). " + ex.Message, "Import error");
            return new IImportService.ImportResult(false, ex.Message);
        }
    }

    // --- decision -----------------------------------------------------------------------------

    private async Task<ImportPlan?> DecidePlanAsync(ImportRequest request)
    {
        var files = request.Files;
        var kind = ImportAnalyzer.ClassifyKind(files);

        return kind switch
        {
            ImportFileKind.Project => await DecideProjectPlanAsync(request),
            ImportFileKind.Gif => new ImportPlan(ImportMode.Gif, [new ImportGroup(GroupName(files[0]), files)]),
            // A .piskel already carries its own layers and frames, so there is nothing to ask about: one
            // file is one sprite, wherever it was dropped.
            ImportFileKind.LayeredDocument => new ImportPlan(ImportMode.LayeredDocument,
                files.Select(f => new ImportGroup(GroupName(f), [f])).ToList()),
            ImportFileKind.Raster => await DecideRasterPlanAsync(request),
            _ => throw new NotSupportedException("Unsupported file type for import.")
        };
    }

    private async Task<ImportPlan?> DecideProjectPlanAsync(ImportRequest request)
    {
        var projectFile = request.Files.First(ImportAnalyzer.IsProject);
        var groups = new[] { new ImportGroup(GroupName(projectFile), [projectFile]) };

        var ext = (projectFile.Extension ?? string.Empty).ToLowerInvariant();

        // .pxm has no multi-sprite scene to unpack -> keep the legacy open/replace behavior.
        if (ext != ".pix2d")
            return new ImportPlan(ImportMode.OpenAsProject, groups);

        // From the command (file picker) we import the project's sprites into the current scene.
        if (!request.FromDrag)
            return new ImportPlan(ImportMode.ProjectIntoScene, groups);

        // On drag, offer to open it as a separate project instead of importing.
        var openAsProject = await dialogService.ShowYesNoDialog(
            $"Open \"{projectFile.Title}\" as a project, or import its sprites into the current scene?",
            "Open Pix2d project",
            okLabel: "Open as project",
            cancelLabel: "Import into scene");

        return new ImportPlan(openAsProject ? ImportMode.OpenAsProject : ImportMode.ProjectIntoScene, groups);
    }

    private async Task<ImportPlan?> DecideRasterPlanAsync(ImportRequest request)
    {
        var files = request.Files;
        var groups = ImportAnalyzer.DetectAnimationGroups(files);

        // A numbered sequence is an unambiguous animation -> apply directly.
        if (groups.Any(g => g.OrderedFiles.Count > 1))
            return new ImportPlan(ImportMode.AnimationFrames, groups);

        // No numbered sequence: each file is a standalone still.
        var hasCurrentSprite = appState.CurrentProject.CurrentEditedNode is Pix2dSprite;
        var insideCurrent = request.DropWorldPosition == null || IsInsideCurrentSprite(request.DropWorldPosition);
        var positionalDefault = hasCurrentSprite && insideCurrent ? ImportMode.Layers : ImportMode.NewSprites;

        // A single image is decided by position alone.
        if (files.Count == 1)
            return new ImportPlan(positionalDefault, groups);

        // Several standalone images are ambiguous (layers vs. separate sprites vs. frames) -> ask.
        var chosen = await dialogService.ShowDialogAsync(new ImportOptionsDialogView(
            summary: $"{files.Count} images",
            defaultMode: positionalDefault,
            allowLayers: hasCurrentSprite));

        if (chosen == null)
            return null;

        return new ImportPlan(chosen.Value, groups);
    }

    private bool IsInsideCurrentSprite(SKPoint? worldPos)
    {
        if (worldPos == null)
            return false;
        if (appState.CurrentProject.CurrentEditedNode is not Pix2dSprite sprite)
            return false;
        return sprite.GetBoundingBox().Contains(worldPos.Value);
    }

    // --- execution ----------------------------------------------------------------------------

    private async Task<IImportService.ImportResult> ExecuteAsync(ImportPlan plan, ImportRequest request)
    {
        switch (plan.Mode)
        {
            case ImportMode.Layers:
                return await ImportAsLayersAsync(request.Files);
            case ImportMode.NewSprites:
                return await ImportAsNewSpritesAsync(request.Files);
            case ImportMode.AnimationFrames:
                return await ImportAsAnimationsAsync(plan.Groups);
            case ImportMode.Gif:
            case ImportMode.LayeredDocument:
                return await ImportWholeDocumentsAsync(request.Files);
            case ImportMode.ProjectIntoScene:
                return await ImportProjectIntoSceneAsync(plan.Groups[0].OrderedFiles[0]);
            case ImportMode.OpenAsProject:
                await projectService.OpenFilesAsync([plan.Groups[0].OrderedFiles[0]]);
                return new IImportService.ImportResult(true);
            default:
                return new IImportService.ImportResult(false, "Unknown import mode");
        }
    }

    private async Task<IImportService.ImportResult> ImportAsLayersAsync(IReadOnlyList<IFileContentSource> files)
    {
        if (appState.CurrentProject.CurrentNodeEditor is not IImportTarget target)
            return new IImportService.ImportResult(false, "Import target is required");

        var bitmaps = await DecodeBitmapsAsync(files);
        if (bitmaps.Count == 0)
            return new IImportService.ImportResult(false, "no images decoded");

        // One layer per image.
        target.Import(BuildLayeredImportData(bitmaps));
        return new IImportService.ImportResult(true);
    }

    private async Task<IImportService.ImportResult> ImportAsNewSpritesAsync(IReadOnlyList<IFileContentSource> files)
    {
        var imports = new List<(string Name, ImportData Data)>();
        foreach (var file in files)
        {
            var bitmaps = await DecodeBitmapsAsync([file]);
            if (bitmaps.Count == 0)
                continue;
            imports.Add((GroupName(file), BuildFramedImportData(bitmaps)));
        }

        if (imports.Count == 0)
            return new IImportService.ImportResult(false, "no images decoded");

        editService.AddArtboardsFromImportData(imports);
        return new IImportService.ImportResult(true);
    }

    private async Task<IImportService.ImportResult> ImportAsAnimationsAsync(IReadOnlyList<ImportGroup> groups)
    {
        var imports = new List<(string Name, ImportData Data)>();
        foreach (var group in groups)
        {
            var bitmaps = await DecodeBitmapsAsync(group.OrderedFiles);
            if (bitmaps.Count == 0)
                continue;
            imports.Add((group.Name, BuildFramedImportData(bitmaps)));
        }

        if (imports.Count == 0)
            return new IImportService.ImportResult(false, "no images decoded");

        editService.AddArtboardsFromImportData(imports);
        return new IImportService.ImportResult(true);
    }

    /// <summary>
    /// One sprite per file for formats whose importer already produces a complete sprite — a GIF (one layer,
    /// N frames) or a .piskel (N layers, N frames each). The decoded <see cref="ImportData"/> is passed
    /// through untouched, which is what preserves a .piskel's layer names and opacity; the raster paths
    /// deliberately rebuild it instead, because there each file is only one bitmap.
    /// </summary>
    private async Task<IImportService.ImportResult> ImportWholeDocumentsAsync(IReadOnlyList<IFileContentSource> files)
    {
        var imports = new List<(string Name, ImportData Data)>();
        foreach (var file in files)
            imports.Add((GroupName(file), await DecodeAsync([file])));

        if (imports.Count == 0)
            return new IImportService.ImportResult(false, "no documents decoded");

        editService.AddArtboardsFromImportData(imports);
        return new IImportService.ImportResult(true);
    }

    private async Task<IImportService.ImportResult> ImportProjectIntoSceneAsync(IFileContentSource projectFile)
    {
        var scene = await ProjectUnpacker.LoadProjectScene(projectFile)
                    ?? throw new InvalidOperationException("Failed to load project scene.");
        editService.InsertSpritesFromScene(scene);
        return new IImportService.ImportResult(true);
    }

    // --- decode helpers (reuse the existing importer pipeline) ---------------------------------

    /// <summary>Decodes files into a single ImportData using the registered importer for their extension.</summary>
    private async Task<ImportData> DecodeAsync(IReadOnlyList<IFileContentSource> files)
    {
        var capture = new CapturingImportTarget();
        var result = await importService.ImportAsync(files, capture);
        if (!result.Success)
            throw new InvalidOperationException(result.Message);
        return capture.Captured ?? throw new InvalidOperationException("Decoder produced no data.");
    }

    /// <summary>Decodes each file independently into a single bitmap (its first frame).</summary>
    private async Task<List<SKBitmap>> DecodeBitmapsAsync(IReadOnlyList<IFileContentSource> files)
    {
        var result = new List<SKBitmap>();
        foreach (var file in files)
        {
            var data = await DecodeAsync([file]);
            var bitmap = data.Layers
                .SelectMany(l => l.Frames)
                .Select(f => f.BitmapProviderFunc?.Invoke())
                .FirstOrDefault(b => b != null);
            if (bitmap != null)
                result.Add(bitmap);
        }
        return result;
    }

    private static ImportData BuildFramedImportData(IReadOnlyList<SKBitmap> bitmaps)
    {
        var size = MaxSize(bitmaps);
        return new ImportData(size, bitmaps.ToList(), replaceFrames: true);
    }

    private static ImportData BuildLayeredImportData(IReadOnlyList<SKBitmap> bitmaps)
    {
        var size = MaxSize(bitmaps);
        var data = new ImportData(size, [], replaceFrames: true);
        data.Layers.Clear();
        foreach (var bitmap in bitmaps)
        {
            data.Layers.Add(new LayerPropertiesInfo
            {
                Frames = [new LayerFrameInfo { BitmapProviderFunc = () => bitmap }]
            });
        }
        return data;
    }

    private static SKSizeI MaxSize(IReadOnlyList<SKBitmap> bitmaps) =>
        new(bitmaps.Max(b => b.Width), bitmaps.Max(b => b.Height));

    private static string GroupName(IFileContentSource file)
    {
        var name = Path.GetFileNameWithoutExtension(file.Path);
        return string.IsNullOrWhiteSpace(name) ? file.Title : name;
    }

    private sealed class CapturingImportTarget : IImportTarget
    {
        public ImportData? Captured { get; private set; }
        public void Import(ImportData data) => Captured = data;
    }
}
