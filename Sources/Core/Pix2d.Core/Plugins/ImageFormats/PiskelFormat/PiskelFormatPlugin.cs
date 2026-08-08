using System.Diagnostics.CodeAnalysis;
using Pix2d.Abstract.Services;

namespace Pix2d.Plugins.ImageFormats.PiskelFormat;

/// <summary>
/// Import-only support for Piskel's <c>.piskel</c> documents (roadmap H2.3) — a migration path for Piskel
/// users. There is no exporter counterpart: writing the format back would only serve moving art *out* of
/// Pix2d, and Piskel itself is effectively unmaintained.
/// </summary>
[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(PiskelFormatPlugin))]
public class PiskelFormatPlugin(IImportService importService) : IPix2dPlugin
{
    public void Initialize()
    {
        importService.RegisterImporter<PiskelImporter>(".piskel", () => new PiskelImporter());
    }
}
