using Avalonia.Styling;
using Pix2d.Command;
using Pix2d.Plugins.Sprite.Commands;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

public class TopBarView : LocalizedComponentBase
{
    protected override StyleGroup BuildStyles() =>
    [
        new Style<BlurPanel>(s => s.OfType<BlurPanel>().Name("central-panel"))
            .ColSpan(3),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<BlurPanel>(s => s.OfType<BlurPanel>().Name("central-panel"))
                .Col(1)
                .ColSpan(1),

            //new Style<AppButton>(s => s.Is<AppButton>().Name("export-button"))
            //    .IsVisible(false),

            //new Style<AppButton>(s => s.Is<AppButton>())
            //    .Width(40)
            //    .Height(40),
        }
    ];

    protected override object Build() =>
        new Grid()
            .Cols("Auto,*,Auto")
            .Margin(12, 12, 12, 0)
            .Children(
                //MENU BUTTON
                new BlurPanel().Col(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Content(
                        new AppButton()
                            .Label(L("Menu"))
                            .Content("\xe91d")
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Command(() => ViewCommands.ToggleMainMenuCommand)
                    ),
                //CENTRAL BLOCK
                new BlurPanel().Name("central-panel")
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                new AppButton()
                                    .Command(() => SpriteEditCommands.Clear)
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Label(L("Clear"))
                                    .Content("\xe90f"),
                                new AppButton()
                                    .Name("export-button")
                                    .Label(L("Export"))
                                    .Command(() => ViewCommands.ShowExportDialogCommand)
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Content("\xe907"),
                                new AppToggleButton()
                                    .IsChecked(() => AppState.UiState.ShowExtraTools,
                                        v => AppState.UiState.ShowExtraTools = v)
                                    .Label(L("Tools"))
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Content("\xe909")
                            )
                    ),
                //UNDO REDO BLOCK
                new BlurPanel().Name("undo-panel").Col(2)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                new AppButton().Col(1)
                                    .Command(EditCommands.Undo)
                                    .Label(L("Undo"))
                                    .Content(
                                        new Grid()
                                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                                            .VerticalAlignment(VerticalAlignment.Stretch)
                                            .Width(50)
                                            .Height(30)
                                            .Children(
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                                                    .Margin(new Thickness(0, 0, 4, 4))
                                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                                    .VerticalAlignment(VerticalAlignment.Top)
                                                    .FontSize(12)
                                                    .Foreground(Colors.Gray.ToBrush())
                                                    .Text(() => UndoSteps.ToString()),
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                                    .Text("\xe90b")
                                            )),
                                new AppButton().Col(1)
                                    .Label(L("Redo"))
                                    .Command(() => EditCommands.Redo)
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Content("\xe90d")
                            )
                    )
            );

    [Inject] private IOperationService OperationService { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;
    private SpriteEditCommands SpriteEditCommands => CommandService.GetCommandList<SpriteEditCommands>()!;
    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;
    private EditCommands EditCommands => CommandService.GetCommandList<EditCommands>()!;

    public int UndoSteps => OperationService?.UndoOperationsCount ?? 0;

    protected override void OnAfterInitialized()
    {
        Messenger.Register<OperationInvokedMessage>(this, msg => StateHasChanged());
        Messenger.Register<ProjectLoadedMessage>(this, _ => StateHasChanged());
    }
}