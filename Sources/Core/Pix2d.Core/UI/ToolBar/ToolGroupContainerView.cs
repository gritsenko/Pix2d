using System.Linq;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.UI.Resources;
using Pix2d.UI.Styles;

namespace Pix2d.UI.ToolBar;

public partial class ToolGroupContainerView : ViewBase<ToolGroupContainerView.State>
{
    public ToolGroupContainerView(AppState appState, IServiceProvider serviceProvider)
        : base(new State(appState, serviceProvider))
    {
    }

    protected override StyleGroup? BuildStyles() =>
    [
        new Style<ToolItemView>()
            .Width(44)
            .Height(44)
            .Margin(6),

        new Style<StackPanel>(s => s.OfType<ToolGroupContainerView>().Descendant().OfType<StackPanel>())
            .Orientation(Orientation.Vertical),

        new Style<Shape>(s => s.Class("toolbar-button").Descendant().Is<Shape>())
            .Fill(StaticResources.Brushes.ForegroundBrush.ToImmutable()),

        new Style<Shape>(s => s.Class("selected").Descendant().Is<Shape>())
            .Fill(Brushes.White.ToImmutable()),

        new Style<TextBlock>(x => x.Class("ToolIcon"))
            .Height(26)
            .Width(26)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .TextAlignment(TextAlignment.Center)
            .FontSize(22),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<StackPanel>(s => s.OfType<ToolGroupContainerView>().Descendant().OfType<StackPanel>())
                .Orientation(Orientation.Horizontal)
        }

    ];

    protected override object Build(State state) =>
        new Border()
            .Classes("Panel")
            .Child(
                new StackPanel()
                    .Ref(out _itemsPanel)
            );

    private StackPanel _itemsPanel = null!;

    protected override void OnAfterInitialized()
    {
        ViewModel!.AttachItemsPanel(_itemsPanel);
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IServiceProvider _serviceProvider;
        private StackPanel? _itemsPanel;
        private string _currentGroup = string.Empty;

        public State(AppState appState, IServiceProvider serviceProvider)
        {
            _appState = appState;
            _serviceProvider = serviceProvider;

            _appState.ToolsState.WatchFor(x => x.ActiveToolGroup, () => ReloadItems(_appState.ToolsState.ActiveToolGroup));
            _appState.UiState.WatchFor(x => x.ShowToolGroup, () => ReloadItems(_appState.ToolsState.ActiveToolGroup));
        }

        public void AttachItemsPanel(StackPanel itemsPanel)
        {
            _itemsPanel = itemsPanel;
            ReloadItems(_appState.ToolsState.ActiveToolGroup, true);
        }

        private void ReloadItems(string group, bool force = false)
        {
            if (_itemsPanel == null)
                return;

            if (!force && _currentGroup == group)
                return;

            _currentGroup = group;
            var items = _appState.ToolsState.Tools.Where(x => x.GroupName == group && x.ShowInToolbar);
            _itemsPanel.Children.Clear();
            foreach (var item in items)
            {
                _itemsPanel.Children.Add(ActivatorUtilities.CreateInstance<ToolItemView>(_serviceProvider, item));
            }
        }
    }
}
