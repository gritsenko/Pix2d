using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;

namespace Pix2d.Abstract.Services
{
    public interface IExportService
    {
        void ExportSelectedNode();
        //void ExportAssets();
        //void BuildProject();
        //void RunProject();
        Task ExportNodesAsync(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender, double scale);
    }
}