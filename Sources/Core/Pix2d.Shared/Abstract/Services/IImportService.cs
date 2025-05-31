#nullable enable
using System.Collections.Immutable;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Services;

public interface IImportService
{
    ImmutableList<string> SupportedExtensions { get; }
    Task<ImportResult> ImportAsync(IEnumerable<IFileContentSource> files, IImportTarget importTarget);
    void RegisterImporter<TImporter>(string extension, Func<IImporter> importerProviderFunc);

    public record ImportResult(bool Success, string Message = "");

    public record ImporterInfo(string Id, string Name, Type ImporterType, string Extension, Func<IImporter> CreateInstanceFunc);
}