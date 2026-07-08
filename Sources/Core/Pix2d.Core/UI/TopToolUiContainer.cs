using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

public partial class TopToolUiContainer(AppState appState) : ViewBase<TopToolUiContainer.State>(new State(appState))
{
    #region Markup

    protected override object Build(State state) =>
        // Horizontal scroll host so a wide tool UI (e.g. the selection-tool clipboard actions + slider)
        // stays reachable on a narrow phone-portrait screen instead of overflowing off both edges. The
        // host stretches to the available width (see the Stretch placement in MainView) while the pill
        // itself stays content-sized and centred when it fits, and scrolls when it doesn't.
        new ScrollViewer()
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .IsVisible(state, x => x.HasToolUiContent)
            // Bound to the window width so the bar scrolls instead of overflowing (its grid column's
            // width is contaminated by the bottom side-panels, so it can't bound the scroll host itself).
            .ClampMaxWidthToViewport(StaticResources.Measures.PanelMargin * 2)
            .Content(
                new BlurPanel()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Content(state, x => x.ToolUiContent!));

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