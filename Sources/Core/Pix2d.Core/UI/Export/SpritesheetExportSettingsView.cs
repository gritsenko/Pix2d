using System.Globalization;
using Pix2d.Abstract.Export;
using Pix2d.Plugins.PngFormat.Exporters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pix2d.UI.Export;

public partial class SpritesheetExportSettingsView() : ViewBase<SpritesheetExportSettingsView.State>(new State()), IExportSettingsViewBase<SpritesheetImageExporter>
{
    protected override object Build(State state) =>
        new StackPanel() //Exporter options
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Children(
                new TextBlock()
                    .Text("Max columns"),
              
                new NumericUpDown()
                    .PlaceholderText("Columns count")
                    .Minimum(1)
                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                    .Increment(1)
                    .Value(state, x => x.MaxColumns, BindingMode.TwoWay)
            ); // exporter options

    private SpritesheetImageExporter _exporter = null!;

    public SpritesheetImageExporter Exporter
    {
        get => _exporter;
        set
        {
            _exporter = value;
            ViewModel?.SetExporter(value);
        }
    }

    public sealed partial class State : ObservableObject
    {
        private SpritesheetImageExporter? _exporter;

        [ObservableProperty]
        public partial decimal MaxColumns { get; set; } = 1;

        public void SetExporter(SpritesheetImageExporter exporter)
        {
            _exporter = exporter;
            MaxColumns = exporter.MaxColumns;
        }

        partial void OnMaxColumnsChanged(decimal value)
        {
            if (_exporter != null)
            {
                _exporter.MaxColumns = (int)value;
            }
        }
    }
}