using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Primitives;
using Pix2d.UI.BrushSettings;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolBarView : ComponentBase
{
    protected override object Build()
    {
        return new StackPanel().Ref(out var parentStackPanel)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                new Button() //Color picker button
                    .Classes("color-button")
                    .IsVisible(() => IsSpriteEditMode)
                    .Command(Commands.View.ToggleColorEditorCommand)
                    .CornerRadius(25)
                    .BorderThickness(3)
                    .BorderBrush(Colors.White.ToBrush())
                    .With(ButtonStyle)
                    .Background(AppState.DrawingState.CurrentColor,
                        bindingMode: BindingMode.OneWay,
                        converter: StaticResources.Converters.SKColorToBrushConverter,
                        bindingSource: AppState.DrawingState),

                new Button() //Brush settings button
                    .Classes("toolbar-button")
                    .Classes("brush-button")
                    .IsVisible(() => IsSpriteEditMode)
                    .Padding(0)
                    .Command(Commands.View.ToggleBrushSettingsCommand)
                    .Content(AppState.DrawingState.CurrentBrushSettings, BindingMode.OneWay,
                        bindingSource: AppState.DrawingState)
                    .With(ButtonStyle)
                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                    .ContentTemplate(
                        new FuncDataTemplate<Primitives.Drawing.BrushSettings>((itemVm, ns) =>
                            new BrushItemView().Preset(itemVm))),
                new StackPanel().Ref(out _toolsStackPanel)
                    .Orientation(parentStackPanel.Orientation, bindingSource: parentStackPanel)
            );
    }


    [Inject] private IToolService ToolService { get; set; }
    [Inject] private AppState AppState { get; set; }
    [Inject] private IMessenger Messenger { get; set; }

    private bool IsSpriteEditMode => AppState.CurrentProject.CurrentContextType == EditContextType.Sprite;

    private StackPanel _toolsStackPanel;

    public List<ToolItemView> Tools { get; set; } = new();


    protected override void OnAfterInitialized()
    {
        //Messenger.Register<EditContextChangedMessage>(this, msg => UpdateToolsFromCurrentContext());
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
        AppState.CurrentProject.WatchFor(x => x.CurrentContextType, OnEditContextChanged);
        //UpdateToolsFromCurrentContext(false);
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

    //private void OnSelectToolCommandExecute(ToolItemViewModel item)
    //{
    //    SessionLogger.OpLog("Select " + item.ToolKey + " tool");

    //    if (CurrentTool.Key != item.ToolKey)
    //    {
    //        ToolService.ActivateTool(item.ToolKey);

    //        AppState.UiState.ShowToolProperties = item.HasToolProperties;
    //    }
    //    else
    //    {
    //        ToggleSelectedToolSettings(item);
    //    }
    //}

    //private void ToggleSelectedToolSettings(ToolItemViewModel item)
    //{
    //    if (item.HasToolProperties)
    //        AppState.UiState.ShowToolProperties = !UiState.ShowToolProperties;
    //    else
    //        AppState.UiState.ShowToolProperties = false;
    //}
}