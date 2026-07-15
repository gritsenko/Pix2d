#nullable enable
using System.Collections.Immutable;
using System.IO;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
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

    public async Task ExportNodesAsync(IEnumerable<SKNode> nodesToRender, double scale, ExporterInfo exporterInfo)
    {
        try
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(AppState.CurrentProject.Title);
            //on android there is an Issue: If file with the same name already exists, save file picker generates incorrect suggested filename (adds suffix to extension) so this file will not have valid extension
            //so we add timestamp to filename
            if (platformStuffService.CurrentPlatform == PlatformType.Android)
                fileName += "_" + DateTime.Now.ToString("s").Replace(":", "").Replace("-", "");

            var exporter = exporterInfo.CreateInstanceFunc();

            await exporter.ExportAsync(nodesToRender, scale);
            Messenger.Send<ProjectExportedMessage>(null!);
        }
        catch (OperationCanceledException)
        {
            // User cancelled the operation, do nothing
        }
        catch (Exception e)
        {
            HandleExportException(e);
        }
    }

    public async Task ExportNodesAsync(IEnumerable<SKNode> nodesToRender, double scale, IExporter exporter)
    {
        try
        {
            await exporter.ExportAsync(nodesToRender, scale);
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
}