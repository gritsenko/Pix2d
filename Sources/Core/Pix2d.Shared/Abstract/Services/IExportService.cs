using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;

namespace Pix2d.Abstract.Services;

public interface IExportService
{
    IReadOnlyList<IExporter> Exporters { get; }
    Task ExportNodesAsync(IEnumerable<SKNode> nodesToRender, double scale, IExporter exporter);

    Task ExportNodesToFileAsync(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender, double scale);
    IEnumerable<SKNode> GetNodesToExport(double scale);
}