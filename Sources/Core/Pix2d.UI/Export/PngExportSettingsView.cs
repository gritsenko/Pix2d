﻿using Pix2d.Abstract.Export;
using Pix2d.Plugins.PngFormat.Exporters;

namespace Pix2d.UI.Export;

public class PngExportSettingsView : ComponentBase, IExportSettingsViewBase<PngImageExporter>
{
    protected override object Build() =>
        new StackPanel() //Exporter options
            .Children(
                new TextBlock().Text("No extra settings yet")
            ); // exporter options

    public PngImageExporter Exporter { get; set; }
}