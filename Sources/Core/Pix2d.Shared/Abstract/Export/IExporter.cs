using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SkiaNodes;

namespace Pix2d.Abstract.Export;

public interface IExporter
{
    string Title { get; }
    Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1);
    
    string[] SupportedExtensions { get; }
}

public interface IStreamExporter : IExporter
{
    Task<Stream> ExportToStreamAsync(IEnumerable<SKNode> nodes, double scale = 1);

}

public interface IFolderPickerExporter : IExporter
{
    Task ExportToFolderAsync(IEnumerable<SKNode> nodes, double scale = 1);

}

public interface IFilePickerExporter : IExporter
{
    Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1);

}