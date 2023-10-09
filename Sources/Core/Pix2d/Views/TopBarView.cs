using Pix2d.Messages;
using Pix2d.Plugins.Sprite;
using Pix2d.Shared;

namespace Pix2d.Views;

public class TopBarView : ComponentBase
{
    private void ButtonStyle(AppButton b) => b
        .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
        .Classes("TopBar");

    protected override object Build() =>
        new Grid()
            .Cols("Auto,*,Auto")
            .Background("#444E59".ToColor().ToBrush())
            .Children(
                //MENU BUTTON
                new StackPanel().Col(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Orientation(Orientation.Horizontal)
                    .Children(
                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Menu")
                            .Content("\xF12B")
                            .Command(Commands.View.ToggleMainMenuCommand),
                        new PromoBlockView()
                    ),

                //CENTRAL BLOCK
                new ScrollViewer()
                    .Col(1)
                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
                    .Content(
                    new StackPanel()
                         .HorizontalAlignment(HorizontalAlignment.Center)
                         .Orientation(Orientation.Horizontal)
                         .Children(

                            new AppButton()
                                .With(ButtonStyle)
                                .Command(SpritePlugin.EditCommands.Clear)
                                .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                                .Label("Clear")
                                .Content("\xE894"),

                            new AppToggleButton()
                                .IsChecked(AppState.UiState.ShowExtraTools, BindingMode.TwoWay, bindingSource: AppState.UiState)
                                .With(ButtonStyle)
                                .Label("Tools")
                                .Content("\xEC7A"),

                            new AppToggleButton()
                                .IsChecked(AppState.UiState.ShowTimeline, BindingMode.TwoWay, bindingSource: AppState.UiState)
                                .With(ButtonStyle)
                                .Label("Animate")
                                .Content("\xED5A"),

                            new AppButton()
                                .With(ButtonStyle)
                                .Label("Export")
                                .Command(Commands.View.ShowExportDialogCommand)
                                .Content("\xEE71")
                        )
                ),
                
                //UNDO REDO BLOCK
                new StackPanel().Col(2)
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        new AppButton().Col(1)
                            .With(ButtonStyle)
                            .Command(Commands.Edit.Undo)
                            .Label("Undo")
                            .Content(

                                new Grid()
                                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                                    .VerticalAlignment(VerticalAlignment.Stretch)
                                    .Width(50)
                                    .Height(30)
                                    .Children(

                                    new TextBlock()
                                        .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                        .VerticalAlignment(VerticalAlignment.Center)
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                        .Text("\xE7A7"),

                                    new TextBlock()
                                        .Margin(new Thickness(0, 0, 4, 4))
                                        .HorizontalAlignment(HorizontalAlignment.Right)
                                        .VerticalAlignment(VerticalAlignment.Top)
                                        .FontSize(12)
                                        .Foreground(Colors.Gray.ToBrush())
                                        .Text(Bind(UndoSteps))
                                )),

                        new AppButton().Col(1)
                            .With(ButtonStyle)
                            .Label("Redo")
                            .Command(Commands.Edit.Redo)
                            .Content("\xE7A6")
                    )
            );

    [Inject] private IOperationService OperationService { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null;
    
    public int UndoSteps => OperationService?.UndoOperationsCount ?? 0;

    protected override void OnAfterInitialized()
    {
        Messenger.Register<OperationInvokedMessage>(this, msg => StateHasChanged());
        Messenger.Register<ProjectLoadedMessage>(this, _ => StateHasChanged());
    }
}