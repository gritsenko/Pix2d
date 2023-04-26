using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Services
{
    public interface IImportService
    {
        //void ImportWithOpenFileDialog();
        void ImportToScene();
        Task ImportToScene(IEnumerable<IFileContentSource> files);

        void RegisterImporterProvider(string extension, Func<IImporter> importerProviderFunc);
        Task TryImportToSprite(IImportTarget targetSprite, params IFileContentSource[] files);
        bool CanImport(string fileExtension);
    }
}