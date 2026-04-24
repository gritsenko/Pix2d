using Pix2d.Abstract.Export;
using Pix2d.Plugins.PngFormat.Exporters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pix2d.UI.Export;

public partial class SpritePngSequenceExporterSettingsView() : ViewBase<SpritePngSequenceExporterSettingsView.State>(new State()), IExportSettingsViewBase<SpritePngSequenceExporter>
{
    protected override object Build(State state) =>
        new StackPanel() //Exporter options
            .Children(
                new TextBlock()
                    .Text("File Name Prefix"),
                new TextBox()
                    .PlaceholderText("Frame_")
                    .Text(state, x => x.Title, BindingMode.TwoWay)
            ); // exporter options

    private SpritePngSequenceExporter _exporter = null!;

    public SpritePngSequenceExporter Exporter
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
        private SpritePngSequenceExporter? _exporter;

        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        public void SetExporter(SpritePngSequenceExporter exporter)
        {
            _exporter = exporter;
            Title = exporter.FileNamePrefix ?? string.Empty;
        }

        partial void OnTitleChanged(string value)
        {
            if (_exporter != null)
            {
                _exporter.FileNamePrefix = value;
            }
        }
    }
}