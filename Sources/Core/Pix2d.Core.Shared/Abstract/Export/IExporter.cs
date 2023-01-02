using System.Collections.Generic;
using System.IO;
using SkiaNodes;

namespace Pix2d.Abstract.Export
{
    public interface IExporter
    {
        Stream Export(IEnumerable<SKNode> nodesToExport = null, double scale = 1);
    }
}