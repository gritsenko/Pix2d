using Pix2d.Exporters;

namespace Pix2d.Views.Export;

public class SpritesheetExportSettingsView : ComponentBase, IExportSettingsViewBase<SpritesheetImageExporter>
{
    protected override object Build() =>
        new StackPanel() //Exporter options
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Children(
                new TextBlock()
                    .Text("Max columns"),
              
                new NumericUpDown()
                    .Watermark("Columns count")
                    .Minimum(1)
                    .Value(Bind(MaxColumns, BindingMode.TwoWay))
            ); // exporter options

    public int MaxColumns
    {
        get => Exporter?.MaxColumns ?? 1;
        set
        {
            if (Exporter != null)
            {
                Exporter.MaxColumns = value;
            }
        }
    }


    public SpritesheetImageExporter Exporter { get; set; }
}