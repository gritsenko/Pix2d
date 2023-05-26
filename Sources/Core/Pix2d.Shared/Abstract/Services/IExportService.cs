using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;

namespace Pix2d.Abstract.Services;

public interface IExportService
{
    IReadOnlyList<IExporter> Exporters { get; }
    void ExportSelectedNode();

    Task ExportNodesAsync(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender, double scale);
}