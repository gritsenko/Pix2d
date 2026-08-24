#nullable enable
using System.Collections.Immutable;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Services;

public class ImportService(AppState appState) : IImportService
{
    private readonly List<IImportService.ImporterInfo> _importerProviders = [];
    public ImmutableList<string> SupportedExtensions => _importerProviders.Select(x=>x.Extension).ToImmutableList();
    public AppState AppState { get; } = appState;


    public void RegisterImporter<TImporter>(string extension, Func<IImporter> importerProviderFunc)
    {
        var importerType = typeof(TImporter);
        _importerProviders.Add(
            new IImportService.ImporterInfo(importerType.Name, importerType.Name, importerType, extension, importerProviderFunc)
        );
    }

    public bool CanImport(string fileExtension) =>
        _importerProviders.Any(x => x.Extension.Equals(fileExtension, StringComparison.InvariantCultureIgnoreCase));

    public async Task<IImportService.ImportResult> ImportAsync(IEnumerable<IFileContentSource> files, IImportTarget importTarget)
    {
        var filesToImport = files.ToImmutableList();

        SessionLogger.OpLog(string.Join(", ", filesToImport.Select(x => x.Path)));

        try
        {
            if (!filesToImport.Any())
                return new IImportService.ImportResult(false, "no files to import");

            if(importTarget == null)
                throw new ArgumentException("no import target provided");

            // An unsupported extension is ordinary user input, not a fault: Android in particular lets
            // the user pick any file (Pix2d has to claim application/octet-stream so its own .pix2d
            // files open at all). Answer it plainly instead of letting GetImporter's First() throw
            // "Sequence contains no matching element" into a dialog — and don't log it as an exception.
            var file = filesToImport.First();
            if (!CanImport(file.Extension))
            {
                var kind = string.IsNullOrWhiteSpace(file.Extension) ? "This file" : $"\"{file.Extension}\" files";
                return new IImportService.ImportResult(false,
                    $"{kind} can't be opened. Pix2d opens: {string.Join(", ", SupportedExtensions)}, .pix2d.");
            }

            var importer = GetImporter(file.Extension);
            await importer.ImportToTargetNode(filesToImport, importTarget);
        }
        catch (Exception ex)
        {
            var fileStr = "";
            var msg = "";
            if (filesToImport.Any())
            {
                fileStr = string.Join(",", filesToImport.Select(x => x.Title));
                msg = "Can't import file(s) " + fileStr + ". Error: " + ex.Message;
            }
            Logger.LogException(ex, msg, fileStr);
            return new IImportService.ImportResult(false, msg);
        }

        return new IImportService.ImportResult(true);
    }

    private IImporter GetImporter(string extension)
    {
        var importer = _importerProviders.First(x =>
            x.Extension.Equals(extension, StringComparison.InvariantCultureIgnoreCase));

        return importer.CreateInstanceFunc();
    }
}