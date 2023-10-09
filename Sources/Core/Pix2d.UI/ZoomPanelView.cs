using Pix2d.Messages.ViewPort;

namespace Pix2d.UI;

public class ZoomPanelView : ComponentBase
{
    protected override object Build() =>
        new Grid()
            .Styles(
                new Style<Button>()
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                )
            .Height(32)
            .Cols("32,*,32")
            .Children(

                new Button().Col(0)
                    .Command(Commands.View.ZoomOut)
                    .Content("-"),

                new Button().Col(1)
                    .Command(Commands.View.ZoomAll)
                    .Content(Bind(CurrentPercentZoom))
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .MinWidth(80),
                
                new Button().Col(2)
                    .Command(Commands.View.ZoomIn)
                    .Content("+")
            );

    [Inject] IViewPortService ViewPortService { get; set; } = null!;
    [Inject] IMessenger Messenger { get; set; } = null!;

    public double CurrentZoom => ViewPortService?.ViewPort?.Zoom ?? 0;
    public string CurrentPercentZoom => (CurrentZoom * 100).ToString("###0") + "%";
    
    protected override void OnAfterInitialized()
    {
        Messenger.Register<ViewPortInitializedMessage>(this, _ => Load());
        Messenger.Register<ViewPortChangedViewMessage>(this, _ => Load());
    }
    
    protected void Load()
    {
        StateHasChanged();
    }
}