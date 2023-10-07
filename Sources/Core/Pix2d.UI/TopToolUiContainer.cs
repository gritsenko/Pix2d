namespace Pix2d.Views;

public class TopToolUiContainer : ComponentBase
{
    protected override object Build() =>
        new Button()
            .Content(() => $"Top tool UI Container {ToolKey}");


    [Inject] public AppState AppState { get; set; } = null!;

    public string ToolKey => AppState.UiState.CurrentToolKey;

    protected override void OnInitialized()
    {
        AppState.UiState.WatchFor(x => x.CurrentToolKey, OnStatePropertyChanged);
    }

    private void OnStatePropertyChanged()
    {
        StateHasChanged();
    }
}