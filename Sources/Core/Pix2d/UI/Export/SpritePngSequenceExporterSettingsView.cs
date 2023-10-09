using Pix2d.Abstract.Export;
using Pix2d.Exporters;

namespace Pix2d.Views.Export;

public class SpritePngSequenceExporterSettingsView : ComponentBase, IExportSettingsViewBase<SpritePngSequenceExporter>
{
    protected override object Build() =>
        new StackPanel() //Exporter options
            .DataContext(Exporter)
            .Children(
                new TextBlock()
                    .Text("File Name Prefix"),

                new TextBox()
                    .Watermark("Frame_")
                    .Text(Bind(Title, BindingMode.TwoWay))
            ); // exporter options

    public string Title
    {
        get => Exporter?.FileNamePrefix ?? "";
        set
        {
            if (Exporter != null)
            {
                Exporter.FileNamePrefix = value;
            }
        }
    }

    public SpritePngSequenceExporter Exporter { get; set; }
}