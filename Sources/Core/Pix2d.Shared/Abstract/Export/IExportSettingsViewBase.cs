namespace Pix2d.Abstract.Export;

public interface IExportSettingsViewBase<TExporter>
    where TExporter : IExporter
{

    TExporter Exporter { get; set; }
}