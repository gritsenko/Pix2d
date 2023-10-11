using Pix2d.Drawing.Tools;
using Pix2d.Messages;
using Pix2d.Plugins.Drawing.Tools;
using Pix2d.UI.Resources;
using Pix2d.UI.ToolBar.Tools;

namespace Pix2d.UI.ToolBar;

public class ToolSettingsContainerView : ComponentBase
{

    public IDataTemplate BrushToolSettingsTemplate { get; } =
        new FuncDataTemplate<BrushTool>((vm, ns) => new BrushToolSettingsView());
    public IDataTemplate EraserToolSettingsTemplate { get; } =
        new FuncDataTemplate<EraserTool>((vm, ns) => new Grid());

    public IDataTemplate FillToolSettingsTemplate { get; } =
        new FuncDataTemplate<FillTool>((vm, ns) => new FillToolSettingsView(vm));

    public IDataTemplate PixelSelectToolSettingsTemplate { get; } =
        new FuncDataTemplate<PixelSelectTool>((vm, ns) => new SelectionToolSettingsView());

    protected override object Build() =>
        new ContentControl()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .DataTemplates(
                EraserToolSettingsTemplate,
                BrushToolSettingsTemplate,
                FillToolSettingsTemplate,
                PixelSelectToolSettingsTemplate
            )
            .Content(() => AppState.CurrentProject.CurrentTool);

    [Inject] AppState AppState { get; set; } = null!;
    [Inject] IMessenger Messenger { get; set; } = null!;

    protected override void OnAfterInitialized()
    {
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
    }

}