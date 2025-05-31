using System.Linq;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

public class TopToolUiContainer : ComponentBase
{
    #region Markup

    protected override object Build() =>
        new BlurPanel()
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
        var currentTool = AppState.ToolsState.Tools.FirstOrDefault(x => x.Name == AppState.ToolsState.CurrentToolKey);
        var toolUiProvider = currentTool?.TopBarUi;

        ToolUiContent = toolUiProvider?.Invoke() as Control;
        
        if(ToolUiContent != null)
            ToolUiContent.DataContext = currentTool.ToolInstance;
        
        StateHasChanged();
    }
}