using Avalonia.Styling;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Views.BrushSettings;
using System.Collections.Generic;
using System.Linq;
using Pix2d.Primitives;

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
                    .With(ButtonStyle)
                    .Background(AppState.DrawingState.CurrentColor, 
                        bindingMode: BindingMode.OneWay, 
                        converter: StaticResources.Converters.SKColorToBrushConverter,
                        bindingSource: AppState.DrawingState),

                new Button() //Brush settings button
                    .Classes("toolbar-button")
                    .Classes("brush-button")
                    .IsVisible(IsSpriteEditMode)
                    .Margin(0, 8)
                    .Padding(0)
                    .Command(Commands.View.ToggleBrushSettingsCommand)
                    .Content(AppState.DrawingState.CurrentBrushSettings, BindingMode.OneWay, bindingSource: AppState.DrawingState)
                    .With(ButtonStyle)
                    .ContentTemplate(new FuncDataTemplate<Primitives.Drawing.BrushSettings>((itemVm, ns) => new BrushItemView().Preset(itemVm))),

                new ItemsControl() //tools list
                    .ItemsSource(AppState.UiState.Tools.Where(x=>x.Context == EditContextType.Sprite))
                    .ItemTemplate(new FuncDataTemplate<ToolState>((item, ns) => new ToolItemView(item)))
            );


    [Inject] private IToolService ToolService { get; set; }
    [Inject] private AppState AppState { get; set; }
    [Inject] private IMessenger Messenger { get; set; }

    private bool IsSpriteEditMode = true;

    public List<ToolItemView> Tools { get; set; } = new();


    protected override void OnAfterInitialized()
    {
        //Messenger.Register<EditContextChangedMessage>(this, msg => UpdateToolsFromCurrentContext());
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
        //UpdateToolsFromCurrentContext(false);
    }
    
    private void ButtonStyle(Button b)
    {
        if(b.Command is Pix2dCommand pc)
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