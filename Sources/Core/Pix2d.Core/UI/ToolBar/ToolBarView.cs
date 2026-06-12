using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Command;
using Pix2d.UI.BrushSettings;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI.ToolBar;

public partial class ToolBarView(AppState appState, ICommandService commandService, IServiceProvider serviceProvider)
    : ViewBase<ToolBarView.State>(new State(appState, commandService, serviceProvider))
{

    protected override StyleGroup BuildStyles() =>
    [
        //general
        new Style<TextBlock>(x => x.Class("ToolIcon"))
            .Height(26)
            .Width(26)
            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .TextAlignment(TextAlignment.Center)
            .FontSize(22),

        new Style<Button>(s => s.Class("toolbar-button"))
            .Margin(6)
            .Width(44)
            .Height(44)
            .Foreground(StaticResources.Brushes.IconForegroundBrush)
            .Background(Brushes.Transparent) //to intrecept poiner events
            .Padding(new Thickness(0)),

        new Style<Shape>(s => s.Class("toolbar-button").Descendant().Is<Shape>())
            .Fill(StaticResources.Brushes.IconForegroundBrush.ToImmutable()),

        new Style<Shape>(s => s.Class("selected").Descendant().Is<Shape>())
            .Fill(Brushes.White.ToImmutable()),

        new Style<Button>(s => s.Class("brush-button"))
            .Background(StaticResources.Brushes.BrushButtonBrush)
            .Margin(new Thickness(0, 8))
            .CornerRadius(12),

        new Style<Button>(s => s.Class("selected"))
            .BorderThickness(1)
            .Foreground(Brushes.White)
            .BorderBrush(StaticResources.Brushes.SelectedToolBorderBrush)
            .Background(StaticResources.Brushes.SelectedToolBrush),

        new Style<Button>(s => s.Class("color-button"))
            .Width(32).Height(32).Margin(new Thickness(0, 16,0,8)),

        new Style<StackPanel>(s => s.OfType<StackPanel>().Class("brush-panel"))
            .Width(56),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<ScrollViewer>(s => s.OfType<ToolBarView>().Descendant().OfType<ScrollViewer>())
                .VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
                .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden),

            new Style<Button>(s => s.Class("toolbar-button"))
                .Padding(new Thickness(0)).VerticalAlignment(VerticalAlignment.Top),

            new Style<Button>(s => s.OfType<Button>().Class("color-button"))
                .Width(32).Height(32).Margin(8, 12, 8, 6).VerticalAlignment(VerticalAlignment.Top),

            new Style<StackPanel>(s => s.OfType<StackPanel>().Name("tools-panel"))
                .Orientation(Orientation.Horizontal),
        }
    ];

    private void ButtonToolTipSetter(Button b)
    {
        if (b.Command is Pix2dCommand pc) b.ToolTip_Tip(pc.Tooltip);
    }

    protected override object Build(State state) =>
        new Grid()
            .Rows("Auto, *")
            .Children(

                new BlurPanel().Row(0)
                    .Margin(0, 0, 0, 12)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Content(
                        new StackPanel()
                            .Classes("brush-panel")
                            .Children(
                            new Button() //Color picker button
                                .Classes("color-button")
                                .IsVisible(state, x => x.IsSpriteEditMode)
                                .Command(state.ViewCommands.ToggleColorEditorCommand)
                                .CornerRadius(32)
                                .BorderThickness(1)
                                .BorderBrush(Colors.White.WithAlpha(0.3f).ToBrush().ToImmutable())
                                .With(ButtonToolTipSetter)
                                .Background(state, x => x.CurrentColorBrush),

                            new Button() //Brush settings button
                                .Classes("toolbar-button")
                                .Classes("brush-button")
                                .IsVisible(state, x => x.IsSpriteEditMode)
                                .Padding(0)
                                .Command(state.ViewCommands.ToggleBrushSettingsCommand)
                                .Content(state, x => x.CurrentBrushSettings)
                                .With(ButtonToolTipSetter)
                                .VerticalContentAlignment(VerticalAlignment.Stretch)
                                .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                                .ContentTemplate(
                                    new FuncDataTemplate<Primitives.Drawing.BrushSettings>(
                                        (itemVm, ns) =>
                                            new BrushItemView()
                                                .ShowSizeText(true)
                                                .Preset(itemVm)))
                        )
                    ),

                new BlurPanel().Row(1)
                    .Content(
                        new ScrollViewer()
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                            .Content(
                                new StackPanel().Ref(out _toolsStackPanel).Name("tools-panel")
                            )
                    )
            );

    private StackPanel _toolsStackPanel = null!;

    protected override void OnAfterInitialized()
    {
        ViewModel!.AttachToolsPanel(_toolsStackPanel);
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IServiceProvider _serviceProvider;
        private StackPanel? _toolsStackPanel;

        public ViewCommands ViewCommands { get; }

        [ObservableProperty]
        public partial bool IsSpriteEditMode { get; set; }

        [ObservableProperty]
        public partial IBrush CurrentColorBrush { get; set; } = Brushes.Transparent;

        [ObservableProperty]
        public partial Primitives.Drawing.BrushSettings? CurrentBrushSettings { get; set; }

        public State(AppState appState, ICommandService commandService, IServiceProvider serviceProvider)
        {
            _appState = appState;
            _serviceProvider = serviceProvider;
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;

            SyncFromAppState();

            _appState.WatchForCurrentProject(x => x.CurrentContextType, OnEditContextChanged);
            _appState.SpriteEditorState.WatchFor(x => x.CurrentColor, SyncFromAppState);
            _appState.SpriteEditorState.WatchFor(x => x.CurrentBrushSettings, SyncFromAppState);
        }

        public void AttachToolsPanel(StackPanel toolsStackPanel)
        {
            _toolsStackPanel = toolsStackPanel;
            RebuildTools();
        }

        public void SyncFromAppState()
        {
            IsSpriteEditMode = _appState.CurrentProject.CurrentContextType == EditContextType.Sprite;
            CurrentColorBrush = _appState.SpriteEditorState.CurrentColor.ToBrush();
            CurrentBrushSettings = _appState.SpriteEditorState.CurrentBrushSettings;
        }

        private void OnEditContextChanged()
        {
            SyncFromAppState();
            RebuildTools();
        }

        private void RebuildTools()
        {
            if (_toolsStackPanel == null)
                return;

            _toolsStackPanel.Children.Clear();

            var groupItems = new List<ToolItemGroupView>();
            var tools = _appState.ToolsState.Tools.Where(x =>
                x.Context == _appState.CurrentProject.CurrentContextType &&
                x.ShowInToolbar);
            foreach (var tool in tools)
            {
                var toolItemView = ActivatorUtilities.CreateInstance<ToolItemView>(_serviceProvider, tool);
                if (string.IsNullOrWhiteSpace(tool.GroupName))
                {
                    _toolsStackPanel.Children.Add(toolItemView);
                    continue;
                }

                var groupItem = groupItems.FirstOrDefault(x => x.GroupName == tool.GroupName);
                if (groupItem != null)
                    continue;

                groupItem = ActivatorUtilities.CreateInstance<ToolItemGroupView>(_serviceProvider);
                groupItem.GroupName = tool.GroupName;
                groupItems.Add(groupItem);
                groupItem.SetActiveItem(tool);
                _toolsStackPanel.Children.Add(groupItem);
            }
        }
    }
}
