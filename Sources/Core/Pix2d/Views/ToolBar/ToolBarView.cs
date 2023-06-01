using Avalonia.Styling;
using Pix2d.Abstract.Tools;
using Pix2d.Messages.Edit;
using Pix2d.Messages;
using Pix2d.Views.BrushSettings;
using System.Collections.Generic;

namespace Pix2d.Views.ToolBar;

public class ToolBarView : ComponentBase
{
    public ToolBarView()
    {
        Selector WideButtonSelector(Selector s) => s.Class("wide").Descendant().OfType<Button>();
        Selector SmallButtonSelector(Selector s) => s.Class("small").Descendant().OfType<Button>();

        this.Styles.AddRange(new IStyle[]
        {
            new Style<Button>(s => WideButtonSelector(s).Class("toolbar-button")).Width(52).Height(52),
            new Style<Button>(s => WideButtonSelector(s).Class("color-button")).Width(40).Height(40),

            new Style<Button>(s => SmallButtonSelector(s).Class("toolbar-button")).Width(40).Height(40),
            new Style<Button>(s => SmallButtonSelector(s).Class("color-button")).Width(32).Height(32),
            new Style<TextBlock>(
                s => SmallButtonSelector(s).Class("color-button").OfType<TextBlock>().Class("ToolIcon")).FontSize(16)
        });
    }

    protected override object Build() =>
        new StackPanel()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(

                new Button() //Color picker button
                    .Classes("color-button")
                    .IsVisible(IsSpriteEditMode)
                    .Margin(0, 8)
                    .Command(Commands.View.ToggleColorEditorCommand)
                    .CornerRadius(25)
                    .BorderThickness(3)
                    .BorderBrush(Colors.White.ToBrush())
                    //.Background(Bind(GetViewModel<ColorPickerViewModel>(), m => m.SelectedColor)
                    //    .Converter(StaticResources.Converters.SKColorToBrushConverter))
                    ,

                new Button() //Brush settings button
                    .Classes("toolbar-button")
                    .IsVisible(IsSpriteEditMode)
                    .Background("#414953".ToColor().ToBrush())
                    .Margin(0, 8)
                    .Padding(0)
                    .Command(Commands.View.ToggleBrushSettingsCommand)
                    .Content(AppState.DrawingState.CurrentBrushSettings)
                    .ContentTemplate(new FuncDataTemplate<Primitives.Drawing.BrushSettings>((itemVm, ns) => new BrushItemView().Preset(itemVm))),

                new ItemsControl() //tools list
                    .ItemsSource(Tools)
            );


    [Inject] private IToolService ToolService { get; set; }
    [Inject] private AppState AppState { get; set; }
    [Inject] private IMessenger Messenger { get; set; }

    private bool IsSpriteEditMode = true;

    private EditContextType EditContextType => AppState.CurrentProject.CurrentContextType;
    private ITool CurrentTool => AppState.CurrentProject.CurrentTool;
    public List<ToolItemView> Tools { get; set; } = new();


    protected override void OnAfterInitialized()
    {
        Messenger.Register<EditContextChangedMessage>(this, msg => UpdateToolsFromCurrentContext());
        Messenger.Register<CurrentToolChangedMessage>(this, ToolChanged);
        UpdateToolsFromCurrentContext(false);
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

    private void ToolChanged(CurrentToolChangedMessage message)
    {
        //SelectedToolSettings?.Deactivated();

        foreach (var item in Tools)
        {
            item.IsSelected = item.ToolKey == CurrentTool.Key;

            //if (item.IsSelected)
            //    SelectedToolItem = item;
        }

        //SelectedToolSettings = SelectedToolItem?.GetSettingsVm();

        //if (SelectedToolSettings != null)
        //{
        //    SelectedToolSettings.Tool = CurrentTool;
        //    SelectedToolSettings.Activated();
        //}

    }

    private void UpdateToolsFromCurrentContext(bool updateActiveTool = true)
    {
        OnPropertyChanged(nameof(EditContextType));

        Tools.Clear();

        var tools = ToolService.GetTools(EditContextType);

        foreach (var toolType in tools)
        {
            //Tools.Add(new ToolItemViewModel(toolType.Name, callback));
        }

        if (updateActiveTool)
            ToolService.ActivateDefaultTool();
    }
}