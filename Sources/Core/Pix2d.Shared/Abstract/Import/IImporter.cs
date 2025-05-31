using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Import;

public interface IImporter
{
    Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget targetNode);
}
