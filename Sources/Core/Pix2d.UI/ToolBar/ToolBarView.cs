using System.Linq;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Pix2d.Command;
using Pix2d.Common.Extensions;
using Pix2d.Messages;
using Pix2d.Primitives;
using Pix2d.UI.BrushSettings;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using SkiaSharp;

namespace Pix2d.UI.ToolBar;

public class ToolBarView : ComponentBase
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
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .Padding(new Thickness(0)),

        new Style<Shape>(s => s.Class("toolbar-button").Descendant().Is<Shape>())
            .Fill(StaticResources.Brushes.ForegroundBrush.ToImmutable()),

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

    protected override object Build() =>
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
                                .IsVisible(() => IsSpriteEditMode)
                                .Command(ViewCommands.ToggleColorEditorCommand)
                                .CornerRadius(32)
                                .BorderThickness(1)
                                .BorderBrush(Colors.White.WithAlpha(0.3f).ToBrush().ToImmutable())
                                .With(ButtonStyle)
                                .Background(() => AppState.SpriteEditorState.CurrentColor.ToBrush()),

                            new Button() //Brush settings button
                                .Classes("toolbar-button")
                                .Classes("brush-button")
                                .IsVisible(() => IsSpriteEditMode)
                                .Padding(0)
                                .Command(ViewCommands.ToggleBrushSettingsCommand)
                                .Content(()=>AppState.SpriteEditorState.CurrentBrushSettings)
                                .With(ButtonStyle)
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


    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;

    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>();

    private bool IsSpriteEditMode => AppState.CurrentProject.CurrentContextType == EditContextType.Sprite;

    private StackPanel _toolsStackPanel = null!;

    public List<ToolItemView> Tools { get; set; } = [];


    protected override void OnAfterInitialized()
    {
        //Messenger.Register<EditContextChangedMessage>(this, msg => UpdateToolsFromCurrentContext());
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
        AppState.CurrentProject.WatchFor(x => x.CurrentContextType, OnEditContextChanged);
        AppState.SpriteEditorState.WatchFor(x => x.CurrentColor, StateHasChanged);
        AppState.SpriteEditorState.WatchFor(x => x.CurrentBrushSettings, StateHasChanged);
        RebuildTools();
    }

    private void OnEditContextChanged()
    {
        RebuildTools();
        StateHasChanged();
    }

    private void RebuildTools()
    {
        _toolsStackPanel.Children.Clear();

        var groupItems = new List<ToolItemGroupView>();

        foreach (var tool in AppState.ToolsState.Tools.Where(x => x.Context == AppState.CurrentProject.CurrentContextType))
        {
            var toolItemView = new ToolItemView(tool);
            if (string.IsNullOrWhiteSpace(tool.GroupName))
                _toolsStackPanel.Children.Add(toolItemView);
            else
            {
                var groupItem = groupItems.FirstOrDefault(x => x.GroupName == tool.GroupName);
                if (groupItem == null)
                {
                    groupItem = new ToolItemGroupView() { GroupName = tool.GroupName };
                    groupItems.Add(groupItem);
                    groupItem.SetActiveItem(tool);
                    _toolsStackPanel.Children.Add(groupItem);
                }
            }
        }
    }

    private void ButtonStyle(Button b)
    {
        if (b.Command is Pix2dCommand pc)
        {
            b.ToolTip(pc.Tooltip);
        }
    }
}