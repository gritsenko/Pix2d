using Pix2d.Abstract.Platform;
using Pix2d.Plugins.ImageFormats.SvgFormat.Exporters;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Plugins.ImageFormats.SvgFormat;

//prevent from being trimmed by AOT compiler
[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(SvgFormatPlugin))]
public class SvgFormatPlugin(IExportService exportService, IFileService fileService) : IPix2dPlugin
{
    public void Initialize()
    {
        exportService.RegisterExporter<SvgImageExporter>("SVG vector image", () => new SvgImageExporter(fileService));
    }
}