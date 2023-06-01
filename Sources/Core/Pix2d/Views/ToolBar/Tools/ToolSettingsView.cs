using Pix2d.Drawing.Tools;
using Pix2d.Plugins.Drawing.Tools;

namespace Pix2d.Views.ToolBar.Tools;

public class ToolSettingsView : ComponentBase
{

    protected override object Build() =>
        new ContentControl()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .DataTemplates(
                BrushToolSettingsTemplate,
                FillToolSettingsTemplate,
                PixelSelectToolSettingsTemplate
            )
        //.Content(
        //    @vm.SelectedToolSettings
        //)
        ;

    public IDataTemplate BrushToolSettingsTemplate { get; } =
        new FuncDataTemplate<BrushTool>((vm, ns) => new BrushToolSettingsView());

    public IDataTemplate FillToolSettingsTemplate { get; } =
        new FuncDataTemplate<FillTool>((vm, ns) => new FillToolSettingsView());

    public IDataTemplate PixelSelectToolSettingsTemplate { get; } =
        new FuncDataTemplate<PixelSelectTool>((vm, ns) => new SelectionToolSettingsView());
}