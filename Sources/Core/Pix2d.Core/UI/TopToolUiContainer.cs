using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

public partial class TopToolUiContainer(AppState appState) : ViewBase<TopToolUiContainer.State>(new State(appState))
{
    #region Markup

    protected override object Build(State state) =>
        new BlurPanel()
            .IsVisible(state, x => x.HasToolUiContent)
            .Content(state, x => x.ToolUiContent!);

    #endregion

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasToolUiContent))]
        public partial Control? ToolUiContent { get; set; }

        public bool HasToolUiContent => ToolUiContent != null;

        public State(AppState appState)
        {
            _appState = appState;

            _appState.ToolsState.WatchFor(x => x.CurrentToolKey, UpdateToolUiContent);
            UpdateToolUiContent();
        }

        private void UpdateToolUiContent()
        {
            var currentTool = _appState.ToolsState.Tools.FirstOrDefault(x => x.Name == _appState.ToolsState.CurrentToolKey);
            var control = currentTool?.TopBarUi?.Invoke() as Control;

            if (control != null && currentTool != null)
            {
                control.DataContext = currentTool.ToolInstance;
            }

            ToolUiContent = control;
        }
    }
}