using Pix2d.Exporters;

namespace Pix2d.Views.Export;

public class PngExportSettingsView : ComponentBase, IExportSettingsViewBase<PngImageExporter>
{
    protected override object Build() =>
        new StackPanel() //Exporter options
            .Children(
                new TextBlock().Text("No extra settings yet")
            ); // exporter options

    public PngImageExporter Exporter { get; set; }
}