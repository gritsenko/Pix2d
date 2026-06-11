using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

/// <summary>
/// Desktop-only strip of open-project tabs (MainView UiGrid Row 0). Each tab shows the project
/// title plus a dirty marker and a close button; the trailing "+" opens a new blank project tab.
/// </summary>
public partial class ProjectTabsView(
    AppState appState,
    IMessenger messenger,
    IProjectService projectService,
    IProjectActivationService projectActivationService,
    IPlatformStuffService platformStuffService)
    : ViewBase<ProjectTabsView.State>(new State(appState, messenger, projectService, projectActivationService,
        platformStuffService))
{
    protected override StyleGroup BuildStyles() =>
    [
        new Style<ListBoxItem>()
            .CornerRadius(6)
            .Padding(10, 2, 4, 3)
            .MinHeight(26)
    ];

    protected override object Build(State state) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .IsVisible(state.ShowTabs)
            .Margin(12, 8, 12, 0)
            .Children(
                new ListBox()
                    .Background(Brushes.Transparent)
                    .BorderThickness(0)
                    .Padding(0)
                    .ItemsPanel(new StackPanel().Orientation(Orientation.Horizontal).Spacing(4))
                    .ItemsSource(state.Tabs)
                    .SelectedIndex(state, x => x.SelectedIndex, BindingMode.TwoWay)
                    .ItemTemplate(new FuncDataTemplate<TabItemViewModel>((itemVm, _) =>
                        itemVm == null
                            ? new TextBlock().Text("")
                            : new StackPanel()
                                .Orientation(Orientation.Horizontal)
                                .Children(
                                    new TextBlock()
                                        .VerticalAlignment(VerticalAlignment.Center)
                                        .FontSize(12)
                                        .Text(itemVm, vm => vm.DisplayTitle),
                                    new Button()
                                        .Margin(6, 0, 0, 0)
                                        .Padding(4, 0, 4, 1)
                                        .Background(Brushes.Transparent)
                                        .BorderThickness(0)
                                        .FontSize(11)
                                        .VerticalAlignment(VerticalAlignment.Center)
                                        .Content("✕")
                                        .ToolTip_Tip(L("Close tab"))
                                        .OnClick(_ => state.CloseTab(itemVm))
                                ))),
                new Button()
                    .Margin(6, 0, 0, 0)
                    .Padding(8, 0, 8, 2)
                    .CornerRadius(6)
                    .Background(Brushes.Transparent)
                    .BorderThickness(0)
                    .FontSize(16)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Content("+")
                    .ToolTip_Tip(L("New tab"))
                    .OnClick(_ => state.NewTab())
            );

    public sealed partial class TabItemViewModel(ProjectState project) : ObservableObject
    {
        public ProjectState Project { get; } = project;

        [ObservableProperty]
        public partial string DisplayTitle { get; set; } = "";

        public void Refresh() =>
            DisplayTitle = (Project.Title ?? "New project") + (Project.HasUnsavedChanges ? " •" : "");
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IProjectService _projectService;
        private readonly IProjectActivationService _projectActivationService;
        private bool _isSyncing;

        public bool ShowTabs { get; }

        public BulkAddObservableCollection<TabItemViewModel> Tabs { get; } = [];

        [ObservableProperty]
        public partial int SelectedIndex { get; set; } = -1;

        public State(AppState appState, IMessenger messenger, IProjectService projectService,
            IProjectActivationService projectActivationService, IPlatformStuffService platformStuffService)
        {
            _appState = appState;
            _projectService = projectService;
            _projectActivationService = projectActivationService;

            ShowTabs = platformStuffService.SupportsMultipleProjects;
            if (!ShowTabs)
                return;

            messenger.Register<ProjectsListChangedMessage>(this, _ => Rebuild());
            messenger.Register<ProjectActivatedMessage>(this, _ => SyncSelection());
            // A fresh load can rename the current tab (file gets assigned) and a save clears the
            // dirty marker; operations set it.
            messenger.Register<ProjectLoadedMessage>(this, _ => RefreshTabs());
            messenger.Register<ProjectSavedMessage>(this, _ => RefreshTabs());
            messenger.Register<OperationInvokedMessage>(this, _ => RefreshTabs());

            Rebuild();
        }

        partial void OnSelectedIndexChanged(int value)
        {
            if (_isSyncing || value < 0 || value >= _appState.LoadedProjects.Count)
                return;

            _projectActivationService.ActivateProject(_appState.LoadedProjects[value]);
        }

        private void Rebuild()
        {
            _isSyncing = true;
            var tabs = _appState.LoadedProjects.Select(p => new TabItemViewModel(p)).ToList();
            foreach (var tab in tabs)
                tab.Refresh();
            Tabs.ReloadItems(tabs);
            _isSyncing = false;

            SyncSelection();
        }

        private void SyncSelection()
        {
            _isSyncing = true;
            SelectedIndex = _appState.LoadedProjects.IndexOf(_appState.CurrentProject);
            _isSyncing = false;
        }

        private void RefreshTabs()
        {
            foreach (var tab in Tabs)
                tab.Refresh();

            SyncSelection();
        }

        public void CloseTab(TabItemViewModel tab) => _ = _projectService.CloseProjectAsync(tab.Project);

        public void NewTab() => _ = _projectService.CreateNewProjectAsync(new SkiaSharp.SKSize(64, 64));
    }
}
