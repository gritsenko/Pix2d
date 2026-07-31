#nullable enable
using System.Collections.Immutable;
using System.IO;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.Export;
using Pix2d.Messages;
using Pix2d.Plugins.PngFormat.Exporters;
using SkiaNodes;

namespace Pix2d.Services;

public class ExportService(
    AppState appState,
    IMessenger messenger,
    IPlatformStuffService platformStuffService,
    IFileService fileService,
    IDialogService dialogService,
    ICrashReportService? crashReportService = null) : IExportService
{
    /// <summary>Shared folder-memory key for every export destination (see <c>IFileService.contextKey</c>).</summary>
    private const string ExportContextKey = "export";

    private readonly List<ExporterInfo> _exporters = [];

    private IMessenger Messenger { get; } = messenger;
    private AppState AppState { get; } = appState;
    public IReadOnlyList<ExporterInfo> RegisteredExporters => _exporters.ToImmutableList();


    public void RegisterExporter<TExporter>(string? displayName, Func<IExporter> createInstanceFunc)
        where TExporter : IExporter
    {
        var t = typeof(TExporter);
        _exporters.Add(new ExporterInfo(t.Name, displayName ?? t.Name, t, createInstanceFunc));
    }

    public async Task ExportItemsAsync(IReadOnlyList<ExportItem> items, double scale, IExporter exporter)
    {
        try
        {
            if (items.Count == 0)
            {
                dialogService.Alert(L("There's nothing to Export!"), L("Export"));
                return;
            }

            // One artboard through an exporter that produces one file: the familiar Save dialog, now seeded
            // with the artboard's own name. Everything else needs a destination that can hold N outputs.
            if (items.Count == 1 && exporter is IFilePickerExporter filePicker)
                await filePicker.ExportToFileAsync(items[0].Nodes, scale, GetSuggestedFileName(items[0].Name));
            else if (!await ExportToPickedFolderAsync(items, scale, exporter))
                return;

            Messenger.Send<ProjectExportedMessage>(null!);
        }
        catch (OperationCanceledException)
        {
            // User cancelled the operation (e.g. dismissed the save/folder picker), do nothing
        }
        catch (Exception e)
        {
            HandleExportException(e);
        }
    }

    /// <summary>
    /// Batch destination: ask for a folder once, warn before clobbering anything already in it, then write
    /// every item. Returns false when the user backed out.
    /// </summary>
    private async Task<bool> ExportToPickedFolderAsync(IReadOnlyList<ExportItem> items, double scale, IExporter exporter)
    {
        if (exporter is not (IBatchExporter or IStreamExporter))
            throw new InvalidOperationException(
                $"Exporter '{exporter.GetType().Name}' can't export {items.Count} artboards — it implements neither IBatchExporter nor IStreamExporter.");

        var folder = await fileService.GetFolderToExportWithDialogAsync(ExportContextKey)
                     ?? throw new OperationCanceledException("Export folder selection canceled");

        if (!await ConfirmOverwriteAsync(folder, items, exporter))
            return false;

        var extension = exporter.SupportedExtensions.FirstOrDefault() ?? ".png";

        foreach (var item in items)
        {
            if (exporter is IBatchExporter batch)
            {
                // A frame sequence names its own files, so several artboards would collide in one folder —
                // give each its own. A sheet derives every name from the base name and needs no subfolder.
                var target = items.Count > 1 && batch.NeedsOwnFolderPerItem
                    ? await folder.GetSubfolderAsync(item.Name)
                    : folder;

                await batch.ExportToFolderAsync(item.Nodes, scale, target, item.Name);
            }
            else
            {
                var streamExporter = (IStreamExporter)exporter;
                await using var stream = await streamExporter.ExportToStreamAsync(item.Nodes, scale);
                var file = await folder.GetFileSourceAsync(item.Name, extension, overwrite: true);
                await file.SaveAsync(stream);
            }
        }

        return true;
    }

    /// <summary>
    /// A batch export writes fixed, name-derived files, so a re-export into the same folder overwrites by
    /// design (that's what makes it re-runnable). Confirm it first: exactly, when the produced names are
    /// known up front; conservatively on a non-empty folder when the exporter names its own files.
    /// </summary>
    private async Task<bool> ConfirmOverwriteAsync(IWriteDestinationFolder folder, IReadOnlyList<ExportItem> items,
        IExporter exporter)
    {
        var existing = (await folder.GetFilesAsync())
            .Select(x => Path.GetFileName(x.Path))
            .Where(x => !string.IsNullOrEmpty(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existing.Count == 0)
            return true;

        if (exporter is IBatchExporter)
        {
            return await dialogService.ShowYesNoDialog(
                L("Folder is not empty — files with the same names will be overwritten."),
                L("Export"));
        }

        var extension = "." + (exporter.SupportedExtensions.FirstOrDefault() ?? ".png").TrimStart('.');
        var conflicts = items.Count(x => existing.Contains(x.Name + extension));

        if (conflicts == 0)
            return true;

        return await dialogService.ShowYesNoDialog(
            string.Format(L("{0} file(s) in this folder will be overwritten. Continue?"), conflicts),
            L("Export"));
    }

    /// <summary>
    /// Android workaround: when a file with the suggested name already exists, the save picker builds a
    /// broken de-duplicated name (the suffix lands after the extension), so the saved file ends up without
    /// a usable extension. A timestamp keeps every suggestion unique.
    /// </summary>
    private string GetSuggestedFileName(string baseName)
    {
        if (platformStuffService.CurrentPlatform != PlatformType.Android)
            return baseName;

        return baseName + "_" + DateTime.Now.ToString("s").Replace(":", "").Replace("-", "");
    }

    public async Task ExportNodesToFileAsync(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender,
        double scale)
    {
        try
        {
            var pngExporter = new PngImageExporter(fileService);
            await using var pngStream = await pngExporter.ExportToStreamAsync(nodesToRender);
            await fileContentSource.SaveAsync(pngStream);
        }
        catch (Exception e)
        {
            HandleExportException(e);
        }
    }

    /// <summary>
    /// Single failure path for all export entry points. Distinguishes an unwritable/locked destination
    /// (permission denied, file in use, disk full) and an over-large render (SkiaSharp OOM, now surfaced
    /// as a sized <see cref="InvalidOperationException"/> by <c>RenderToBitmap</c>) from the generic
    /// empty-scene case, so the user sees an accurate message instead of the misleading "nothing to
    /// Export!". Every failure is also reported to telemetry with a precise <c>Export</c> source — caught
    /// here at the throwing call, so the captured exception carries a real stack (not a frame-less fatal
    /// that reached a global handler unattributed).
    /// </summary>
    private void HandleExportException(Exception e)
    {
        switch (e)
        {
            case UnauthorizedAccessException:
            case IOException:
                dialogService.Alert(
                    "Couldn't save the file — the location isn't writable or the file is in use. Try exporting to a different folder.",
                    "Export");
                break;
            case OutOfMemoryException:
            // "Out of memory …" is our sized message from RenderToBitmap; "allocate pixels" is
            // SkiaSharp's raw wording when the SKBitmap/SKCanvas ctor throws before the guard's check.
            case { } when e.Message.Contains("Out of memory", StringComparison.OrdinalIgnoreCase)
                          || e.Message.Contains("allocate pixels", StringComparison.OrdinalIgnoreCase):
                dialogService.Alert(
                    "Couldn't export — the image or export scale is too large to fit in memory. Try a smaller size or scale.",
                    "Export");
                break;
            default:
                dialogService.Alert("There's nothing to Export!", "Export");
                break;
        }

        Logger.Log(e.Message);
        crashReportService?.CaptureHandled(e, "Export");
    }

    public IEnumerable<SKNode> GetNodesToExport(double scale)
    {
        if (AppState.CurrentProject.CurrentEditedNode == null)
            yield break;

        yield return AppState.CurrentProject.CurrentEditedNode;
    }

    public int GetArtboardsCount() => GetArtboards().Count;

    public IReadOnlyList<ExportItem> GetExportItems(ExportScope scope)
    {
        var artboards = GetArtboards();

        if (scope == ExportScope.AllSprites && artboards.Count > 0)
            return artboards.Select(x => CreateItem(x, artboards.Count)).ToList();

        // "Selected" means the node-level artboard selection of the General (objects) context. The Sprite
        // context has no node selection (its selection is a pixel marquee), so fall back to the artboard
        // being edited — which is exactly what export covered before batch mode existed.
        var selected = (AppState.CurrentProject.Selection?.Nodes ?? [])
            .OfType<Pix2dSprite>()
            .ToHashSet();

        if (selected.Count > 0)
            return artboards.Where(selected.Contains) // scene order, not click order
                .Select(x => CreateItem(x, artboards.Count))
                .ToList();

        var current = AppState.CurrentProject.CurrentEditedNode;
        return current == null ? [] : [CreateItem(current, artboards.Count)];
    }

    private List<Pix2dSprite> GetArtboards() =>
        AppState.CurrentProject.SceneNode?.Nodes.OfType<Pix2dSprite>().ToList() ?? [];

    /// <summary>
    /// Names one export item. With several artboards in the scene the artboard's own name is the only thing
    /// that tells the outputs apart, so it always wins. A single-artboard project *is* its artwork, so the
    /// saved project's file name is the better name there — falling back to the artboard name (and finally
    /// to "untitled") for a project that has never been saved.
    /// </summary>
    private ExportItem CreateItem(SKNode node, int artboardsCount)
    {
        var nodeName = ExportFileNames.Sanitize(node.Name);

        if (artboardsCount > 1)
            return new ExportItem(nodeName.Length > 0 ? nodeName : ExportFileNames.Fallback, [node]);

        var projectName = ExportFileNames.Sanitize(
            Path.GetFileNameWithoutExtension(AppState.CurrentProject.FileName ?? string.Empty));

        var name = projectName.Length > 0 ? projectName
            : nodeName.Length > 0 ? nodeName
            : ExportFileNames.Fallback;

        return new ExportItem(name, [node]);
    }
}
