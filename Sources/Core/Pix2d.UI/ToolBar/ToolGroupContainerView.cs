using Pix2d.Messages;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolGroupContainerView : ComponentBase
{
    protected override object Build() =>
        new StackPanel()
            .Ref(out _itemsPanel)
            .Background(StaticResources.Brushes.MainBackgroundBrush);

    [Inject] AppState AppState { get; set; } = null!;
    [Inject] IMessenger Messenger { get; set; } = null!;

    private StackPanel _itemsPanel;

    protected override void OnAfterInitialized()
    {
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
    }

}