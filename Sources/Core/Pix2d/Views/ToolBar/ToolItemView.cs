using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;

namespace Pix2d.Views.ToolBar;

public class ToolItemView : ComponentBase
{
    private ToolState toolState;

    protected override object Build() =>
        new Grid().Children(
            new Border()
                .BorderThickness(4, 0, 0, 0)
                .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                .Background(StaticResources.Brushes.SelectedItemBrush)
                .IsVisible(() => IsSelected),

            new Button()
                .Classes("toolbar-button")
                .OnClick(OnButtonClicked)
                .CommandParameter(new Binding())
                .Background(Colors.Transparent.ToBrush())
                .Content(() => ToolKey)
                .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                .ToolTip(() => ToolState?.ToolTip),

            new Path()
                .Data(Geometry.Parse("F1 M 4,0L 4,4L 0,4"))
                .Fill(Color.Parse("#FFCCCCCC").ToBrush())
                .Width(8)
                .Height(8)
                .Stretch(Stretch.Fill)
                .VerticalAlignment(VerticalAlignment.Bottom)
                .HorizontalAlignment(HorizontalAlignment.Right)
                .IsVisible(new Binding("HasToolProperties"))
        );

    [Inject] IToolService ToolService { get; set; } = null!;
    [Inject] AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    public string ToolKey => ToolState?.Name ?? "";
    public bool IsSelected => AppState.UiState.CurrentToolKey == ToolKey;

    public ToolState ToolState
    {
        get => toolState; set
        {
            toolState = value;
            StateHasChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
    }

    private void OnButtonClicked(RoutedEventArgs args)
    {
        if (IsSelected)
        {
            AppState.UiState.ShowToolProperties = !AppState.UiState.ShowToolProperties;
        }
        else
        {
            AppState.UiState.ShowToolProperties = false;
            ToolService.ActivateTool(this.toolState.Name);
        }

        this.StateHasChanged();
    }
}