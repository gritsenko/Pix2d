using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.Psd;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(PsdPlugin))]
public class PsdPlugin(IImportService importService) : IPix2dPlugin
{
    public IImportService ImportService { get; } = importService;

    public void Initialize()
    {
        ImportService.RegisterImporter<PsdImporter>(".psd", () => new PsdImporter());
    }
}