using Pix2d.Abstract;

namespace Pix2d.Plugins.Pxm
{
    public class PxmPlugin : IPix2dPlugin
    {
        public IImportService ImportService { get; }

        public PxmPlugin(IImportService importService)
        {
            ImportService = importService;
        }

        public void Initialize()
        {
            ImportService.RegisterImporterProvider(".pxm", () => new PxmImporter());
        }
    }
}
