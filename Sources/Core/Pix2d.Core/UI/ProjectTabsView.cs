using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

/// <summary>
/// Strip of open-project tabs. Occupies its own top row of MainView's RootGrid, so the canvas starts
/// below it instead of being overlapped. Each tab shows the project title plus a dirty marker and a
/// close button; the trailing "+" opens a new blank project tab.
///
/// The strip is shown on every head whose <see cref="IPlatformStuffService.SupportsMultipleProjects"/>
/// is true (desktop and Android). On a phone the bar is far too narrow to hold every tab, so the tab
/// list is laid out by an <see cref="OverflowPanel"/>: the tabs that fit stay on the bar and the rest
/// move behind the trailing "⌄" dropdown, which lists them by name and brings the picked one to the
/// front of the tab order. The active tab is kept on the bar the same way — anything that activates a
/// project whose tab has been pushed off the strip (opening a file, Ctrl+T, closing a neighbour)
/// moves it to the front too, so the selected tab is never invisible.
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
        // Figma "Body 14" tab text tiers: 30% inactive, 60% hover, 90% selected.
        new Style<ListBoxItem>()
            .CornerRadius(6)
            .Padding(10, 2, 4, 3)
            .MinHeight(26)
            .BorderThickness(1)
            .BorderBrush(Brushes.Transparent)
            .Foreground(StaticResources.Brushes.MutedForegroundBrush),

        new Style<ListBoxItem>(s => s.OfType<ListBoxItem>().Class(":pointerover"))
            .Foreground(StaticResources.Brushes.SecondaryForegroundBrush),
        // The Simple theme paints selection/hover on the template's ContentPresenter directly,
        // so plain Background setters on the item are ignored for those states.
        new Style<ContentPresenter>(s =>
                s.OfType<ListBoxItem>().Class(":pointerover").Template().OfType<ContentPresenter>())
            .Background(Colors.White.WithAlpha(0.08f).ToBrush()),

        new Style<ListBoxItem>(s => s.OfType<ListBoxItem>().Class(":selected"))
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .BorderBrush(StaticResources.Brushes.SelectedToolBorderBrush),
        new Style<ContentPresenter>(s =>
                s.OfType<ListBoxItem>().Class(":selected").Template().OfType<ContentPresenter>())
            .Background(StaticResources.Brushes.SelectedItemBrush)
    ];

    /// <summary>
    /// Width the strip keeps free to the right of the tabs for the "+" and overflow buttons. The tab
    /// list is capped to (strip width - this), which is what makes tabs drop out instead of pushing
    /// the buttons off screen. A constant, not the buttons' measured width, on purpose: the overflow
    /// button only exists *because* tabs dropped, so measuring it would feed the layout back into
    /// itself and let the strip oscillate.
    /// </summary>
    private const double TrailingButtonsReserve = 88;

    protected override object Build(State state)
    {
        // Kept as locals (not inline expressions) so the view can wire the panel's overflow reports
        // and clamp the list's width — .ItemsPanel(panel) hands the ListBox this very instance.
        var tabsPanel = new OverflowPanel { Spacing = 4 }
            .ClipToBounds(true);

        tabsPanel.VisibleCountChanged += (_, _) => state.OnVisibleTabCountChanged(tabsPanel.VisibleCount);

        var tabsList = new ListBox()
            .Background(Brushes.Transparent)
            .BorderThickness(0)
            .Padding(0)
            .ItemsPanel(tabsPanel)
            // The template's ScrollViewer would otherwise hand the panel an unbounded width and
            // nothing would ever count as overflow.
            .With(lb => ScrollViewer.SetHorizontalScrollBarVisibility(lb, ScrollBarVisibility.Disabled))
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
                                .FontSize(14)
                                // A long title must not eat the whole strip on a phone.
                                .MaxWidth(140)
                                .TextTrimming(TextTrimming.CharacterEllipsis)
                                .Text(itemVm, vm => vm.DisplayTitle),
                            new Button()
                                .Margin(6, 0, 0, 0)
                                .Padding(4, 0, 4, 1)
                                .Background(Brushes.Transparent)
                                .BorderThickness(0)
                                .FontSize(11)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .Content("\u2715")
                                .ToolTip_Tip(L("Close tab"))
                                .OnClick(_ => state.CloseTab(itemVm)))
                        .With(tab => AttachTabContextMenu(tab, itemVm, state))));

        return new Border()
            .IsVisible(state.ShowTabs)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
            .BorderThickness(0, 0, 0, 1)
            .Padding(12, 6)
            // The buttons follow the last tab rather than sitting at the far right of the window, so
            // the tab list is width-capped here instead of being squeezed by a star grid column.
            .OnSizeChanged(e => tabsList.MaxWidth = Math.Max(0, e.NewSize.Width - 24 - TrailingButtonsReserve))
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(
                        tabsList,

                        new Button()
                            .Margin(6, 0, 0, 0)
                            .Padding(8, 0, 8, 2)
                            .CornerRadius(6)
                            .Background(Brushes.Transparent)
                            .BorderThickness(0)
                            .FontSize(16)
                            .Foreground(StaticResources.Brushes.ForegroundBrush)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Content("+")
                            .ToolTip_Tip(L("New tab"))
                            .OnClick(_ => state.NewTab()),

                        // Overflow dropdown: only the tabs that did not fit, picked one goes first.
                        new Button()
                            .Name("ProjectTabsOverflowButton")
                            .IsVisible(state, x => x.HasHiddenTabs)
                            .Margin(2, 0, 0, 0)
                            .Padding(6, 0, 6, 2)
                            .CornerRadius(6)
                            .Background(Brushes.Transparent)
                            .BorderThickness(0)
                            .Foreground(StaticResources.Brushes.ForegroundBrush)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .ToolTip_Tip(L("More projects"))
                            .Content(
                                new StackPanel()
                                    .Orientation(Orientation.Horizontal)
                                    .Spacing(4)
                                    .Children(
                                        new TextBlock()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .FontSize(12)
                                            // ChevronDown; the UI face (Zed Mono) has no U+2304.
                                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                            .Text("\uE70D"),
                                        new TextBlock()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .FontSize(14)
                                            .Text(state, x => x.HiddenTabsCountLabel)))
                            .Flyout(
                                new MenuFlyout { Placement = PlacementMode.Bottom }
                                    .ItemsSource(state.HiddenTabs)
                                    .ItemTemplate((TabItemViewModel item) =>
                                        new MenuItem()
                                            .Header(item, vm => vm.DisplayTitle)
                                            .OnClick(_ => state.PickHiddenTab(item))))
                    ));
    }

    /// <summary>Hold this long on a tab to get its context menu.</summary>
    private static readonly TimeSpan TabLongPress = TimeSpan.FromMilliseconds(450);

    /// <summary>How far the pointer may drift during a hold before it stops counting as a long press.</summary>
    private const double TabLongPressSlop = 12;

    /// <summary>
    /// Per-tab context menu (rename / close), opened by right-click <b>and</b> by a hand-rolled long
    /// press. Both are needed: touch has no right button, and on desktop Pix2d binds the right mouse
    /// button to the eyedropper app-wide, so a <c>ContextFlyout</c> alone can miss its
    /// <c>ContextRequested</c> — the same reasoning as <c>ActionsBarView.AttachLongPress</c>.
    ///
    /// The menu is opened by the <b>release</b> that ends a long-enough hold, not by a timer while the
    /// finger is still down. Opening it mid-hold does work — but the release that follows immediately
    /// light-dismisses it again (verified on a running desktop build: the menu appeared while the
    /// button was held and was gone the moment it came up), so the user would never get to pick
    /// anything. Showing it from a posted callback keeps it clear of the release that triggered it.
    /// </summary>
    private static void AttachTabContextMenu(Control tab, TabItemViewModel itemVm, State state)
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        flyout.Items.Add(new MenuItem()
            .Header(L("Rename project"))
            .OnClick(_ => state.RenameTab(itemVm)));
        flyout.Items.Add(new MenuItem()
            .Header(L("Close tab"))
            .OnClick(_ => state.CloseTab(itemVm)));

        tab.ContextFlyout = flyout;

        var pressedAt = default(DateTime?);
        var pressedPoint = default(Point);

        tab.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            pressedAt = DateTime.UtcNow;
            pressedPoint = e.GetPosition(tab);
        }, RoutingStrategies.Tunnel);

        tab.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (pressedAt is null)
                return;

            var delta = e.GetPosition(tab) - pressedPoint;
            if (Math.Abs(delta.X) > TabLongPressSlop || Math.Abs(delta.Y) > TabLongPressSlop)
                pressedAt = null;
        }, RoutingStrategies.Tunnel);

        tab.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            var start = pressedAt;
            pressedAt = null;

            if (start is null || DateTime.UtcNow - start.Value < TabLongPress)
                return;

            // Swallow the release so the hold does not also read as a plain tap on the tab.
            e.Handled = true;
            Dispatcher.UIThread.Post(() => flyout.ShowAt(tab));
        }, RoutingStrategies.Tunnel);

        tab.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => pressedAt = null,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    public sealed partial class TabItemViewModel(ProjectState project) : ObservableObject
    {
        public ProjectState Project { get; } = project;

        [ObservableProperty]
        public partial string DisplayTitle { get; set; } = "";

        // Uppercase per the design (Figma textCase: UPPER); Avalonia has no text-transform.
        public void Refresh() =>
            DisplayTitle = (Project.Title ?? "New project").ToUpperInvariant() + (Project.HasUnsavedChanges ? " •" : "");
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IProjectService _projectService;
        private readonly IProjectActivationService _projectActivationService;
        private bool _isSyncing;

        /// <summary>
        /// Leading tabs the strip could actually lay out, as last reported by the panel.
        /// -1 = never arranged, which must not be read as "nothing fits".
        /// </summary>
        private int _visibleTabCount = -1;

        private bool _reorderCheckPending;

        public bool ShowTabs { get; }

        public BulkAddObservableCollection<TabItemViewModel> Tabs { get; } = [];

        /// <summary>Tabs pushed off the strip; the content of the overflow dropdown.</summary>
        public ObservableCollection<TabItemViewModel> HiddenTabs { get; } = [];

        [ObservableProperty]
        public partial int SelectedIndex { get; set; } = -1;

        [ObservableProperty]
        public partial bool HasHiddenTabs { get; set; }

        [ObservableProperty]
        public partial string HiddenTabsCountLabel { get; set; } = "";

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
            UpdateHiddenTabs();
        }

        private void SyncSelection()
        {
            // Recovery sends ProjectLoadedMessage BEFORE ProjectsListChangedMessage, so the first
            // SyncSelection runs while Tabs is still empty (Rebuild only repopulates on the latter).
            // Setting an out-of-range index on the ListBox coerces it to -1 and — via the TwoWay
            // binding — writes -1 back here, leaving the active tab unhighlighted. Skip until the
            // tab list mirrors the project set; Rebuild() always calls us again once it has.
            var index = _appState.LoadedProjects.IndexOf(_appState.CurrentProject);
            if (index < 0 || index >= Tabs.Count)
                return;

            _isSyncing = true;
            SelectedIndex = index;
            _isSyncing = false;

            RequestActiveTabVisibilityCheck();
        }

        private void RefreshTabs()
        {
            foreach (var tab in Tabs)
                tab.Refresh();

            SyncSelection();
        }

        /// <summary>Called by the view when the strip's panel reports a new fitting-tab count.</summary>
        public void OnVisibleTabCountChanged(int visibleCount)
        {
            _visibleTabCount = visibleCount;
            UpdateHiddenTabs();
            RequestActiveTabVisibilityCheck();
        }

        private void UpdateHiddenTabs()
        {
            var hidden = _visibleTabCount < 0
                ? []
                : Tabs.Skip(_visibleTabCount).ToList();

            HiddenTabs.Clear();
            foreach (var tab in hidden)
                HiddenTabs.Add(tab);

            HasHiddenTabs = hidden.Count > 0;
            HiddenTabsCountLabel = hidden.Count > 0 ? hidden.Count.ToString() : "";
        }

        /// <summary>
        /// Picking a hidden project moves it to the head of the tab order, which is what makes it
        /// visible on the strip — the panel always lays the list out from the start.
        /// </summary>
        public void PickHiddenTab(TabItemViewModel tab)
        {
            if (tab != null)
                _projectActivationService.MoveProjectToFrontAndActivate(tab.Project);
        }

        public void CloseTab(TabItemViewModel tab) => _ = _projectService.CloseProjectAsync(tab.Project);

        /// <summary>
        /// Renames the tab's project. Rename always targets the CURRENT project, so the tab is
        /// activated first — which also shows the user which project the dialog is about.
        /// </summary>
        public void RenameTab(TabItemViewModel tab)
        {
            if (tab == null)
                return;

            _projectActivationService.ActivateProject(tab.Project);
            _ = _projectService.RenameCurrentProjectAsync();
        }

        public void NewTab() => _ = _projectService.CreateNewProjectAsync(new SkiaSharp.SKSize(64, 64));

        /// <summary>
        /// Keeps the active tab on the strip. Deferred to Background priority: the fitting count is
        /// only correct after the pending layout pass, and a reorder here re-enters Rebuild.
        /// </summary>
        private void RequestActiveTabVisibilityCheck()
        {
            if (_reorderCheckPending)
                return;

            _reorderCheckPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _reorderCheckPending = false;
                EnsureActiveTabVisible();
            }, DispatcherPriority.Background);
        }

        private void EnsureActiveTabVisible()
        {
            // Unknown fit (never arranged) or everything fits: nothing to do — desktop never
            // reorders tabs behind the user's back.
            if (_visibleTabCount <= 0 || _visibleTabCount >= _appState.LoadedProjects.Count)
                return;

            var index = _appState.LoadedProjects.IndexOf(_appState.CurrentProject);
            if (index < _visibleTabCount)
                return;

            _projectActivationService.MoveProjectToFrontAndActivate(_appState.CurrentProject);
        }
    }
}
