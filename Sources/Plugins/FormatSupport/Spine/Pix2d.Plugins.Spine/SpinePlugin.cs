using Pix2d.Abstract;

namespace Pix2d.Plugins.SpinePlugin
{
    public class SpinePlugin : IPix2dPlugin
    {
        public IImportService ImportService { get; }

        public SpinePlugin(IImportService importService)
        {
            ImportService = importService;
        }

        public void Initialize()
        {
            ImportService.RegisterImporterProvider(".json", () => new SpineImporter());
        }
    }
}
