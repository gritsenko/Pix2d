using Pix2d.Exporters;

namespace Pix2d.Views.Export;

public class PngExportSettingsView : ExportSettingsViewBase
{
    protected override object Build() =>
        new StackPanel() //Exporter options
            .Children(
                new TextBlock().Text("No extra settings yet")
            ); // exporter options

    public override string ExporterName => "Png exporter";
}