#nullable enable
using System.Collections.Immutable;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Messages;
using Pix2d.Plugins.PngFormat.Exporters;
using SkiaNodes;

namespace Pix2d.Services;

public class ExportService(
    AppState appState,
    IMessenger messenger,
    IPlatformStuffService platformStuffService,
    IFileService fileService,
    IDialogService dialogService) : IExportService
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
        catch (Exception e)
        {
            dialogService.Alert("There's nothing to Export!", "Export");
            Logger.Log(e.Message);
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
            dialogService.Alert("There's nothing to Export!", "Export");
            Logger.Log(e.Message);
        }

    }

    public IEnumerable<SKNode> GetNodesToExport(double scale)
    {
        if (AppState.CurrentProject.CurrentEditedNode == null)
            yield break;

        yield return AppState.CurrentProject.CurrentEditedNode;
    }
}