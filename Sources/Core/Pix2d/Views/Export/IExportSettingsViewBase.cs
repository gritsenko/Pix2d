using Pix2d.Abstract.Export;

namespace Pix2d.Views.Export;

public interface IExportSettingsViewBase<TExporter>
    where TExporter : IExporter
{

    TExporter Exporter { get; set; }
}