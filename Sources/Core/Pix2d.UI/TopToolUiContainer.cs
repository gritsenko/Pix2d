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
        AppState.ToolsState.WatchFor(x => x.CurrentToolKey, OnStatePropertyChanged);
    }

    private void OnStatePropertyChanged()
    {
        var toolUiProvider = AppState.ToolsState.Tools
            .FirstOrDefault(x => x.Name == AppState.ToolsState.CurrentToolKey)?.TopBarUi;

        ToolUiContent = toolUiProvider?.Invoke() as Control;

        StateHasChanged();
    }
}