using Pix2d.Abstract.Export;
using Pix2d.Plugins.PngFormat.Exporters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pix2d.UI.Export;

public partial class PngExportSettingsView() : ViewBase<PngExportSettingsView.State>(new State()), IExportSettingsViewBase<PngImageExporter>
{
    protected override object Build(State state) =>
        new StackPanel() //Exporter options
            .Children(
                new TextBlock().Text("No extra settings yet")
            ); // exporter options

    public PngImageExporter Exporter { get; set; } = null!;

    public sealed partial class State : ObservableObject
    {
    }
}