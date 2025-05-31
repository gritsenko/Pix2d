using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.Pxm
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(PxmPlugin))]
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
