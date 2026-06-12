#nullable enable
using System.Threading.Tasks;
using Pix2d.Abstract.Import.Flow;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Orchestrates the multi-mode import flow: classifies the file set, decides the import mode
/// (asking the user only when ambiguous), and executes it (layers / new sprites / animation frames /
/// project insert / open-as-project / gif).
/// </summary>
public interface IImportFlowService
{
    Task<IImportService.ImportResult> RunImportFlowAsync(ImportRequest request);
}
