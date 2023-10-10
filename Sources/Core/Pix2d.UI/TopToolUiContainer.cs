using System.Linq;

namespace Pix2d.UI;

public class TopToolUiContainer : ComponentBase
{
    #region Markup

    protected override object Build() =>
        new ContentControl()
            .IsVisible(() => ToolUiContent != null)
            .Content(() => ToolUiContent);


    #endregion

    [Inject] public AppState AppState { get; set; } = null!;

    public Control? ToolUiContent { get; set; }

    protected override void OnInitialized()
    {
        AppState.UiState.WatchFor(x => x.CurrentToolKey, OnStatePropertyChanged);
    }

    private void OnStatePropertyChanged()
    {
        var toolUiProvider = AppState.UiState.Tools.FirstOrDefault(x => x.Name == AppState.UiState.CurrentToolKey)
            ?.TopBarUI;

        ToolUiContent = toolUiProvider?.Invoke() as Control;

        StateHasChanged();
    }
}