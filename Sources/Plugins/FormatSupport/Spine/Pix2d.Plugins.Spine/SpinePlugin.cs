using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.SpinePlugin
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(SpinePlugin))]
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
